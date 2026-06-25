using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Wms.Inventory.Contracts;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Application.DependencyInjection;
using Wms.Outbound.Application.Features.ConsumeStockAllocated;
using Wms.Outbound.Application.Features.CreateWave;
using Wms.Outbound.Application.Features.ReceiveOutboundOrder;
using Wms.Outbound.Infrastructure.DependencyInjection;
using Wms.Outbound.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Outbound.IntegrationTests;

// What: integration test read-side Outbound (orders/waves/picking-tasks) atas Postgres nyata
// Why: membuktikan reader CQRS (IOutboundOrderReader/IWaveReader/IPickingTaskReader) memenuhi kontrak
// yang dipakai WebUI: list paginated (Skip/Take) + TotalCount lintas seluruh match, filter status,
// detail by id (null saat tak ada), serta agregasi cross-aggregate (LineCount wave, union lines).
// How: harness mirip OutboundFlowTests/GoodsReceiptReaderTests (Postgres Testcontainers, EnsureCreated);
// seed via command REAL (ReceiveOutboundOrder/CreateWave) + consumer StockAllocated untuk PickingTask.
[Collection(PostgresCollection.Name)]
public sealed class OutboundReaderTests(PostgresFixture fixture)
{
    [Fact]
    public async Task OrderReader_lists_paginated_with_total_and_summary_aggregates()
    {
        await using var sp = await BuildOutboundAsync();

        // 3 order New (2 line tiap order → LineCount=2, TotalQty=15)
        for (var i = 0; i < 3; i++)
            await ReceiveOrderAsync(sp, ("SKU-1", 10), ("SKU-2", 5));

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IOutboundOrderReader>();

        var firstPage = await reader.ListAsync(status: "New", page: 1, pageSize: 2);
        Assert.Equal(3, firstPage.TotalCount);   // total seluruh match, bukan ukuran page
        Assert.Equal(2, firstPage.Items.Count);
        Assert.True(firstPage.HasNextPage);
        Assert.All(firstPage.Items, item =>
        {
            Assert.Equal("New", item.Status);
            Assert.Equal(2, item.LineCount);
            Assert.Equal(15, item.TotalQty);
        });

        var secondPage = await reader.ListAsync(status: "New", page: 2, pageSize: 2);
        Assert.Single(secondPage.Items);         // sisa 1 dari 3
        Assert.False(secondPage.HasNextPage);
    }

    [Fact]
    public async Task OrderReader_get_returns_detail_with_lines_and_null_when_missing()
    {
        await using var sp = await BuildOutboundAsync();
        var orderId = await ReceiveOrderAsync(sp, ("SKU-1", 10), ("SKU-2", 5));

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IOutboundOrderReader>();

        var detail = await reader.GetAsync(orderId);
        Assert.NotNull(detail);
        Assert.Equal(orderId, detail!.OrderId);
        Assert.Equal("New", detail.Status);
        Assert.Equal(2, detail.Lines.Count);
        Assert.Contains(detail.Lines, line => line is { Sku: "SKU-1", Qty: 10 });
        Assert.Equal([1, 2], detail.Lines.Select(line => line.Id).ToArray()); // Id = indeks 1-based

        Assert.Null(await reader.GetAsync(Guid.NewGuid())); // tak ada → null
    }

    [Fact]
    public async Task WaveReader_lists_with_order_and_line_counts()
    {
        await using var sp = await BuildOutboundAsync();
        await SetupWaveAsync(sp, ("SKU-1", 10), ("SKU-2", 5)); // 1 order, 2 line

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IWaveReader>();

        var page = await reader.ListAsync(status: "Active", page: 1, pageSize: 20);
        var summary = Assert.Single(page.Items);
        Assert.Equal("Active", summary.Status);
        Assert.Equal(1, summary.OrderCount);
        Assert.Equal(2, summary.LineCount);
    }

    [Fact]
    public async Task WaveReader_get_returns_detail_with_union_lines_and_null_when_missing()
    {
        await using var sp = await BuildOutboundAsync();
        var (orderId, waveId) = await SetupWaveAsync(sp, ("SKU-1", 10), ("SKU-2", 5));

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IWaveReader>();

        var detail = await reader.GetAsync(waveId);
        Assert.NotNull(detail);
        Assert.Equal(waveId, detail!.WaveId);
        Assert.Equal("Active", detail.Status);
        Assert.Contains(orderId, detail.OrderIds);
        Assert.Equal(2, detail.Lines.Count);
        Assert.All(detail.Lines, line => Assert.Equal(orderId, line.OrderId));
        Assert.Contains(detail.Lines, line => line is { Sku: "SKU-1", Qty: 10 });

        Assert.Null(await reader.GetAsync(Guid.NewGuid())); // tak ada → null
    }

    [Fact]
    public async Task PickingTaskReader_lists_filtered_by_wave()
    {
        await using var sp = await BuildOutboundAsync();
        var (orderId, waveId) = await SetupWaveAsync(sp, ("SKU-1", 10));

        await DeliverStockAllocatedAsync(sp, Guid.NewGuid(), new StockAllocatedV1(
            waveId, [new StockAllocationV1(orderId, "SKU-1", "RACK-A1", "B1", 10, Guid.NewGuid())]));

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPickingTaskReader>();

        var page = await reader.ListAsync(waveId: waveId, page: 1, pageSize: 20);
        var task = Assert.Single(page.Items);
        Assert.Equal(waveId, task.WaveId);
        Assert.Equal("SKU-1", task.Sku);
        Assert.Equal(10, task.Qty);
        Assert.Equal("Assigned", task.Status);

        // filter waveId yang berbeda → kosong (TotalCount 0)
        var empty = await reader.ListAsync(waveId: Guid.NewGuid());
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
    }

    // ---- harness (mirror OutboundFlowTests) ----

    private async Task<ServiceProvider> BuildOutboundAsync()
    {
        var sp = new ServiceCollection()
            .AddLogging()
            .AddOutboundApplication()
            .AddOutboundProductCatalogStub()
            .AddOutboundInfrastructure(await fixture.CreateDatabaseAsync())
            .AddLocalMessaging()
            .AddLocalAuditing()
            .BuildServiceProvider();

        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboundDbContext>().Database.EnsureCreatedAsync();
        return sp;
    }

    private static async Task<Guid> ReceiveOrderAsync(ServiceProvider sp, params (string Sku, int Qty)[] lines)
    {
        var command = new ReceiveOutboundOrderCommand(
            "CUST-1", "Jl. Merdeka 1", [.. lines.Select(line => new ReceiveOrderLine(line.Sku, line.Qty))]);
        var result = await SendAsync(sp, command);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<(Guid OrderId, Guid WaveId)> SetupWaveAsync(
        ServiceProvider sp, params (string Sku, int Qty)[] lines)
    {
        var orderId = await ReceiveOrderAsync(sp, lines);
        var waveId = (await SendAsync(sp, new CreateWaveCommand([orderId]))).Value;
        return (orderId, waveId);
    }

    private static async Task DeliverStockAllocatedAsync(ServiceProvider sp, Guid eventId, StockAllocatedV1 message)
    {
        using var scope = sp.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<StockAllocatedConsumer>().HandleAsync(eventId, message);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
    }

    private static async Task<TResponse> SendAsync<TResponse>(ServiceProvider sp, IRequest<TResponse> request)
    {
        using var scope = sp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
