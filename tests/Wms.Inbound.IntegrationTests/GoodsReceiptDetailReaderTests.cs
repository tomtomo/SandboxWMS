using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Application.DependencyInjection;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;
using Wms.Inbound.Application.Features.DeclareScanComplete;
using Wms.Inbound.Application.Features.ScanItem;
using Wms.Inbound.Domain;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Inbound.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Inbound.IntegrationTests;

// What: integration test read-side detail GoodsReceipt — IGoodsReceiptReader.GetByIdAsync atas Postgres nyata
// Why: membuktikan detail read-DTO memproyeksikan owned collections (expected/scanned/discrepancy) DAN
// menurunkan DiscrepancyDetail.Qty secara deterministik (ShortDelivery → magnitude variance per SKU);
// plus kontrak "id tak ditemukan → null" (endpoint memetakan ke 404).
[Collection(PostgresCollection.Name)]
public sealed class GoodsReceiptDetailReaderTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GetByIdAsync_returns_detail_with_lines_and_derived_discrepancy_qty()
    {
        var provider = await BuildAsync();
        await using var _provider = provider;

        // expected 10, scan 6 (Good) → ShortDelivery dengan magnitude variance = 4
        var createResult = await SendAsync(provider, new CreateGoodsReceiptCommand("WH-DET",
            [new CreateGoodsReceiptLine("SKU-A", 10)]));
        var grId = createResult.Value;
        await SendAsync(provider, new ScanItemCommand(grId, "SKU-A", 6, null, null, LineStatus.Good));
        await SendAsync(provider, new DeclareScanCompleteCommand(grId));

        using var scope = provider.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IGoodsReceiptReader>();

        var detail = await reader.GetByIdAsync(grId);

        Assert.NotNull(detail);
        Assert.Equal(grId, detail!.GoodsReceiptId);
        Assert.Equal("WH-DET", detail.WarehouseId);
        Assert.Equal(nameof(GoodsReceiptStatus.Pending), detail.Status);

        var expected = Assert.Single(detail.ExpectedLines);
        Assert.Equal("SKU-A", expected.Sku);
        Assert.Equal(10, expected.ExpectedQty);

        var scanned = Assert.Single(detail.ScannedLines);
        Assert.Equal(6, scanned.ActualQty);
        Assert.Equal(nameof(LineStatus.Good), scanned.LineStatus);

        var discrepancy = Assert.Single(detail.Discrepancies);
        Assert.Equal("SKU-A", discrepancy.Sku);
        Assert.Equal(nameof(DiscrepancyType.ShortDelivery), discrepancy.Type);
        Assert.Equal(4, discrepancy.Qty);   // |received(6) - expected(10)|
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_missing_id()
    {
        var provider = await BuildAsync();
        await using var _provider = provider;

        using var scope = provider.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IGoodsReceiptReader>();

        var detail = await reader.GetByIdAsync(Guid.NewGuid());

        Assert.Null(detail);
    }

    private async Task<ServiceProvider> BuildAsync()
    {
        var objectRoot = Path.Combine(Path.GetTempPath(), "wms-inbound-detail-rdr-" + Guid.NewGuid().ToString("N"));
        var provider = new ServiceCollection()
            .AddLogging()
            .AddInboundApplication()
            .AddInboundProductCatalogStub()
            .AddInboundInfrastructure(await fixture.CreateDatabaseAsync())
            .AddLocalMessaging()
            .AddLocalAuditing()
            .AddLocalObjectStore(objectRoot)
            .BuildServiceProvider();

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<InboundDbContext>().Database.EnsureCreatedAsync();
        return provider;
    }

    private static async Task<TResponse> SendAsync<TResponse>(IServiceProvider provider, IRequest<TResponse> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
