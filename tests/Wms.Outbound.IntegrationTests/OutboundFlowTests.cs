using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Domain.Results;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inventory.Contracts;
using Wms.Outbound.Application.DependencyInjection;
using Wms.Outbound.Application.Features.CompletePicking;
using Wms.Outbound.Application.Features.ConsumeStockAllocated;
using Wms.Outbound.Application.Features.CreateWave;
using Wms.Outbound.Application.Features.DispatchWave;
using Wms.Outbound.Application.Features.ReceiveOutboundOrder;
using Wms.Outbound.Contracts;
using Wms.Outbound.Domain;
using Wms.Outbound.Infrastructure.DependencyInjection;
using Wms.Outbound.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Outbound.IntegrationTests;

// What: integration test slice & consumer Outbound atas Postgres nyata (Phase 03c, ADR-0005/0028)
// Why: membuktikan tiap sisi modul Outbound bekerja end-to-end di Postgres (Testcontainers): CreateWave
// (orders→InProgress + WaveReleased ke Outbox), StockAllocated consumer (→ PickingTask + attach wave +
// idempotency), CompletePicking (→ PickingCompleted + gate Wave→Ready), DispatchWave (→ ShipmentDispatched
// + orders Closed). Lewat pipeline/consumer REAL dalam satu transaksi (anti dual-write).
[Collection(PostgresCollection.Name)]
public sealed class OutboundFlowTests(PostgresFixture fixture)
{
    [Fact]
    public async Task CreateWave_places_orders_inprogress_and_emits_wave_released_to_outbox()
    {
        await using var sp = await BuildOutboundAsync();

        var orderId = await ReceiveOrderAsync(sp, ("SKU-1", 10), ("SKU-2", 5));
        var waveId = (await SendAsync(sp, new CreateWaveCommand([orderId]))).Value;

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();

        var order = await db.OutboundOrders.SingleAsync(o => o.Id == new OutboundOrderId(orderId));
        Assert.Equal(OutboundOrderStatus.InProgress, order.Status);
        Assert.Equal(waveId, order.WaveId);

        var wave = await db.Waves.SingleAsync();
        Assert.Equal(WaveStatus.Active, wave.Status);

        var emitted = Assert.Single(await OutboxAsync(db, WaveReleasedV1.LogicalName));
        var payload = JsonSerializer.Deserialize<WaveReleasedV1>(emitted.Payload)!;
        Assert.Equal(waveId, payload.WaveId);
        Assert.Equal(2, payload.Lines.Count);
        Assert.All(payload.Lines, line => Assert.Equal(orderId, line.OrderId));
        Assert.Contains(payload.Lines, line => line is { Sku: "SKU-1", Qty: 10 });
    }

    [Fact]
    public async Task StockAllocated_creates_picking_task_per_allocation_and_attaches_to_wave()
    {
        await using var sp = await BuildOutboundAsync();
        var (_, waveId) = await SetupWaveAsync(sp, ("SKU-1", 10));

        var stockId = Guid.NewGuid();
        await DeliverStockAllocatedAsync(sp, Guid.NewGuid(), new StockAllocatedV1(
            waveId, [new StockAllocationV1("SKU-1", "RACK-A1", "B1", 10, stockId)]));

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();

        var task = await db.PickingTasks.SingleAsync();
        Assert.Equal(PickingTaskStatus.Assigned, task.Status);
        Assert.Equal(waveId, task.WaveId);
        Assert.Equal(stockId, task.StockId);
        Assert.Equal("RACK-A1", task.SourceLocationId);
        Assert.Equal("SKU-1", task.Sku);
        Assert.Equal(10, task.Qty);

        var wave = await db.Waves.SingleAsync();
        Assert.Contains(task.Id.Value, wave.PickingTaskIds);
    }

    [Fact]
    public async Task StockAllocated_duplicate_delivery_creates_tasks_once()
    {
        await using var sp = await BuildOutboundAsync();
        var (_, waveId) = await SetupWaveAsync(sp, ("SKU-1", 10));

        var eventId = Guid.NewGuid();
        var message = new StockAllocatedV1(waveId, [new StockAllocationV1("SKU-1", "RACK-A1", "B1", 10, Guid.NewGuid())]);
        await DeliverStockAllocatedAsync(sp, eventId, message);
        await DeliverStockAllocatedAsync(sp, eventId, message); // redelivery (at-least-once) — eventId sama

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();
        Assert.Equal(1, await db.PickingTasks.CountAsync());                          // efek sekali (Inbox dedup)
        Assert.Equal(1, await db.Set<InboxMessage>()
            .CountAsync(i => i.HandlerType == StockAllocatedConsumer.HandlerType));   // diproses sekali
    }

    [Fact]
    public async Task CompletePicking_emits_picking_completed_and_marks_wave_ready_only_when_all_done()
    {
        await using var sp = await BuildOutboundAsync();
        var (_, waveId) = await SetupWaveAsync(sp, ("SKU-1", 10), ("SKU-2", 5));

        await DeliverStockAllocatedAsync(sp, Guid.NewGuid(), new StockAllocatedV1(waveId,
        [
            new StockAllocationV1("SKU-1", "RACK-A1", "B1", 10, Guid.NewGuid()),
            new StockAllocationV1("SKU-2", "RACK-A2", "B2", 5, Guid.NewGuid()),
        ]));

        var taskIds = await PickingTaskIdsAsync(sp, waveId);

        // selesaikan task pertama → wave MASIH Active (belum semua selesai)
        Assert.True((await SendAsync(sp, new CompletePickingCommand(taskIds[0], "STG-1"))).IsSuccess);
        Assert.Equal(WaveStatus.Active, await WaveStatusAsync(sp, waveId));

        // selesaikan task kedua → SEMUA selesai → wave Ready
        Assert.True((await SendAsync(sp, new CompletePickingCommand(taskIds[1], "STG-1"))).IsSuccess);
        Assert.Equal(WaveStatus.Ready, await WaveStatusAsync(sp, waveId));

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();
        var emitted = await OutboxAsync(db, PickingCompletedV1.LogicalName);
        Assert.Equal(2, emitted.Count);                                      // satu PickingCompleted per task
        var first = JsonSerializer.Deserialize<PickingCompletedV1>(emitted[0].Payload)!;
        Assert.Equal(waveId, first.WaveId);
        Assert.Equal("STG-1", first.StagingLocationId);
        // ADR-0030: operatorId di-source ICurrentUser (origin-mesin test → SYSTEM) untuk OperatorActivity
        Assert.Equal(SystemActor.Id, first.OperatorId);
    }

    [Fact]
    public async Task DispatchWave_emits_shipment_dispatched_and_closes_orders()
    {
        await using var sp = await BuildOutboundAsync();
        var (orderId, waveId) = await SetupWaveAsync(sp, ("SKU-1", 10));

        await DeliverStockAllocatedAsync(sp, Guid.NewGuid(), new StockAllocatedV1(
            waveId, [new StockAllocationV1("SKU-1", "RACK-A1", "B1", 10, Guid.NewGuid())]));
        var taskIds = await PickingTaskIdsAsync(sp, waveId);
        Assert.True((await SendAsync(sp, new CompletePickingCommand(taskIds[0], "STG-1"))).IsSuccess);

        Assert.True((await SendAsync(sp, new DispatchWaveCommand(waveId))).IsSuccess);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();
        Assert.Equal(WaveStatus.Dispatched, (await db.Waves.SingleAsync()).Status);
        Assert.Equal(OutboundOrderStatus.Closed,
            (await db.OutboundOrders.SingleAsync(o => o.Id == new OutboundOrderId(orderId))).Status);

        var emitted = Assert.Single(await OutboxAsync(db, ShipmentDispatchedV1.LogicalName));
        Assert.Equal(waveId, JsonSerializer.Deserialize<ShipmentDispatchedV1>(emitted.Payload)!.WaveId);
    }

    [Fact]
    public async Task DispatchWave_before_ready_is_rejected()
    {
        await using var sp = await BuildOutboundAsync();
        var (_, waveId) = await SetupWaveAsync(sp, ("SKU-1", 10)); // wave Active, belum picking

        var result = await SendAsync(sp, new DispatchWaveCommand(waveId));

        Assert.True(result.IsFailure);
        Assert.Equal(WaveErrors.InvalidDispatch, result.Error);
    }

    // ---- harness ----

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

    // order baru + wave aktif → kembalikan (orderId, waveId)
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

    private static async Task<IReadOnlyList<Guid>> PickingTaskIdsAsync(ServiceProvider sp, Guid waveId)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();
        return await db.PickingTasks.Where(t => t.WaveId == waveId).Select(t => t.Id.Value).ToListAsync();
    }

    private static async Task<WaveStatus> WaveStatusAsync(ServiceProvider sp, Guid waveId)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();
        return (await db.Waves.SingleAsync(w => w.Id == new WaveId(waveId))).Status;
    }

    private static async Task<List<OutboxMessage>> OutboxAsync(OutboundDbContext db, string logicalName)
        => await db.Set<OutboxMessage>().Where(m => m.LogicalName == logicalName)
            .OrderBy(m => m.OccurredAt).ToListAsync();

    private static async Task<TResponse> SendAsync<TResponse>(ServiceProvider sp, IRequest<TResponse> request)
    {
        using var scope = sp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
