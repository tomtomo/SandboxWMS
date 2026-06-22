using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Domain.Results;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inbound.Contracts;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Application.Features.CompletePutaway;
using Wms.Inventory.Application.Features.ConsumeGoodsReceiptConfirmed;
using Wms.Inventory.Application.Features.ConsumePickingCompleted;
using Wms.Inventory.Application.Features.ConsumeShipmentDispatched;
using Wms.Inventory.Application.Features.ConsumeWaveReleased;
using Wms.Inventory.Contracts;
using Wms.Inventory.Domain;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Persistence;
using Wms.Outbound.Contracts;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Inventory.IntegrationTests;

// What: integration test lifecycle Stock ditransisikan event lintas-context (Phase 03b, ADR-0005/0028)
// Why: membuktikan keempat consumer Inventory bekerja end-to-end di Postgres NYATA (Testcontainers):
// GRConfirmed branch (Good→OnHand+PutawayTask, QcHold→Quarantine tanpa task); WaveReleased→alokasi FEFO
// (expiry terdekat)→StockAllocated ter-emit ke Outbox; PickingCompleted→Picked; ShipmentDispatched→removed;
// dan idempotency (Inbox composite key) — redelivery → transisi SEKALI.
// How: build provider Inventory (infra + adapter Local), EnsureCreated schema; seed Stock via domain
// factory; deliver tiap event lewat consumer REAL dalam scope tersendiri (mensimulasikan delivery rail).
[Collection(PostgresCollection.Name)]
public sealed class StockLifecycleConsumerTests(PostgresFixture fixture)
{
    private const string Warehouse = "WH-JKT";

    [Fact]
    public async Task GrConfirmed_creates_onhand_with_putaway_for_good_and_quarantine_without_for_qchold()
    {
        await using var sp = await BuildInventoryAsync();

        var message = new GRConfirmedV1(
            Guid.NewGuid(), Warehouse, SupplierId: null,
            [
                new ReceivedLineV1("SKU-GOOD", 10, "Good", "B1", new DateOnly(2026, 12, 31)),
                new ReceivedLineV1("SKU-QC", 5, "QcHold", "B2", new DateOnly(2026, 11, 30)),
            ],
            []);
        await DeliverGrConfirmedAsync(sp, Guid.NewGuid(), message);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var good = await db.Stocks.SingleAsync(s => s.Sku == "SKU-GOOD");
        Assert.Equal(StockStatus.OnHand, good.Status);
        Assert.Equal("REC-01", good.LocationId);          // receiving area seed
        Assert.Equal("B1", good.Batch);

        var qc = await db.Stocks.SingleAsync(s => s.Sku == "SKU-QC");
        Assert.Equal(StockStatus.Quarantine, qc.Status);
        Assert.Equal("QC-A", qc.LocationId);              // quarantine area seed

        // satu PutawayTask HANYA untuk Stock OnHand (Good) — QcHold tak generate task
        var task = Assert.Single(await db.PutawayTasks.ToListAsync());
        Assert.Equal(good.Id, task.StockId);
    }

    [Fact]
    public async Task WaveReleased_allocates_earliest_expiry_fefo_and_emits_stock_allocated()
    {
        await using var sp = await BuildInventoryAsync();

        // dua batch SKU-1 Available: expiry berbeda → FEFO harus pilih yang TERDEKAT (earlier)
        var earlierId = await SeedAsync(sp, StockStatus.Available, "SKU-1", "B-EARLY", new DateOnly(2026, 6, 30), 10);
        var laterId = await SeedAsync(sp, StockStatus.Available, "SKU-1", "B-LATE", new DateOnly(2026, 12, 31), 10);

        var waveId = Guid.NewGuid();
        await DeliverWaveReleasedAsync(sp, Guid.NewGuid(),
            new WaveReleasedV1(waveId, [new WaveLineV1(Guid.NewGuid(), "SKU-1", 10)]));

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var earlier = await db.Stocks.SingleAsync(s => s.Id == new StockId(earlierId));
        var later = await db.Stocks.SingleAsync(s => s.Id == new StockId(laterId));
        Assert.Equal(StockStatus.Allocated, earlier.Status);   // FEFO: expiry terdekat dialokasi
        Assert.Equal(waveId, earlier.AllocatedToWaveId);
        Assert.Equal(StockStatus.Available, later.Status);     // yang expiry jauh tetap Available

        // StockAllocated ter-emit ke Outbox (satu transaksi dengan transisi)
        var outbox = await db.Set<OutboxMessage>()
            .Where(m => m.LogicalName == StockAllocatedV1.LogicalName).ToListAsync();
        var emitted = Assert.Single(outbox);
        var payload = JsonSerializer.Deserialize<StockAllocatedV1>(emitted.Payload)!;
        Assert.Equal(waveId, payload.WaveId);
        var allocation = Assert.Single(payload.Allocations);
        Assert.Equal(earlierId, allocation.StockId);
        Assert.Equal("B-EARLY", allocation.Batch);
        Assert.Equal(10, allocation.Qty);
    }

    [Fact]
    public async Task PickingCompleted_transitions_allocated_to_picked_at_staging()
    {
        await using var sp = await BuildInventoryAsync();
        var waveId = Guid.NewGuid();
        var stockId = await SeedAsync(sp, StockStatus.Allocated, "SKU-1", "B1", null, 10, waveId);

        var pickingTaskId = Guid.NewGuid();
        await DeliverPickingCompletedAsync(sp, Guid.NewGuid(),
            new PickingCompletedV1(waveId, pickingTaskId, stockId, "SKU-1", "B1", 10, "STG-1", OperatorId: null));

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var stock = await db.Stocks.SingleAsync(s => s.Id == new StockId(stockId));
        Assert.Equal(StockStatus.Picked, stock.Status);
        Assert.Equal(pickingTaskId, stock.PickingTaskId);
        Assert.Equal("STG-1", stock.LocationId);
    }

    [Fact]
    public async Task ShipmentDispatched_removes_picked_stock_bound_to_wave()
    {
        await using var sp = await BuildInventoryAsync();
        var waveId = Guid.NewGuid();
        await SeedAsync(sp, StockStatus.Picked, "SKU-1", "B1", null, 10, waveId);
        var otherWaveId = Guid.NewGuid();
        var survivorId = await SeedAsync(sp, StockStatus.Picked, "SKU-2", "B2", null, 5, otherWaveId);

        await DeliverShipmentDispatchedAsync(sp, Guid.NewGuid(), new ShipmentDispatchedV1(waveId));

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.False(await db.Stocks.AnyAsync(s => s.AllocatedToWaveId == waveId));  // wave didispatch → removed
        Assert.True(await db.Stocks.AnyAsync(s => s.Id == new StockId(survivorId))); // wave lain tak tersentuh

        // ADR-0030: Inventory emit StockRemovedV1 (pemilik Stock) → Reporting StockOnHandView decrement +
        // DispatchSummary. Hanya stock wave ini, dgn dimensi warehouse/sku/batch/qty yang Outbound tak punya.
        var emitted = Assert.Single(await db.Set<OutboxMessage>()
            .Where(m => m.LogicalName == StockRemovedV1.LogicalName).ToListAsync());
        var payload = JsonSerializer.Deserialize<StockRemovedV1>(emitted.Payload)!;
        Assert.Equal(waveId, payload.WaveId);
        var line = Assert.Single(payload.Lines);
        Assert.Equal(Warehouse, line.WarehouseId);
        Assert.Equal("SKU-1", line.Sku);
        Assert.Equal("B1", line.Batch);
        Assert.Equal(10, line.Qty);
    }

    [Fact]
    public async Task WaveReleased_duplicate_delivery_allocates_once()
    {
        await using var sp = await BuildInventoryAsync();
        await SeedAsync(sp, StockStatus.Available, "SKU-1", "B-EARLY", new DateOnly(2026, 6, 30), 10);
        await SeedAsync(sp, StockStatus.Available, "SKU-1", "B-LATE", new DateOnly(2026, 12, 31), 10);

        var eventId = Guid.NewGuid();
        var wave = new WaveReleasedV1(Guid.NewGuid(), [new WaveLineV1(Guid.NewGuid(), "SKU-1", 10)]);
        await DeliverWaveReleasedAsync(sp, eventId, wave);
        await DeliverWaveReleasedAsync(sp, eventId, wave);   // redelivery (at-least-once) — eventId sama

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(1, await db.Stocks.CountAsync(s => s.Status == StockStatus.Allocated)); // alokasi sekali
        Assert.Equal(1, await db.Set<OutboxMessage>()
            .CountAsync(m => m.LogicalName == StockAllocatedV1.LogicalName));                // emit sekali
        Assert.Equal(1, await db.Set<InboxMessage>()
            .CountAsync(i => i.HandlerType == WaveReleasedConsumer.HandlerType));            // diproses sekali
    }

    [Fact]
    public async Task CompletePutaway_emits_putaway_completed_with_operator()
    {
        await using var sp = await BuildInventoryAsync();

        // seed OnHand stock + PutawayTask Assigned (referensikan stock by id, bukan navigation)
        var stockId = await SeedAsync(sp, StockStatus.OnHand, "SKU-1", "B1", null, 10);
        var taskId = await SeedPutawayTaskAsync(sp, stockId);

        // panggil handler langsung (provider infra-only): real DB + real outbox; handler SaveChanges sendiri.
        using (var scope = sp.CreateScope())
        {
            var services = scope.ServiceProvider;
            var handler = new CompletePutawayHandler(
                services.GetRequiredService<IPutawayTaskRepository>(),
                services.GetRequiredService<IStockRepository>(),
                services.GetRequiredService<IIntegrationEventOutbox>(),
                services.GetRequiredService<ICurrentUser>(),
                services.GetRequiredService<IUnitOfWork>());
            AssertSuccess(await handler.Handle(new CompletePutawayCommand(taskId, "RACK-B12"), default));
        }

        // ADR-0030: PutawayCompletedV1 ter-emit → Reporting OperatorActivity (putaway-count per operator)
        using var verify = sp.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var emitted = Assert.Single(await db.Set<OutboxMessage>()
            .Where(m => m.LogicalName == PutawayCompletedV1.LogicalName).ToListAsync());
        var payload = JsonSerializer.Deserialize<PutawayCompletedV1>(emitted.Payload)!;
        Assert.Equal(taskId, payload.PutawayTaskId);
        Assert.Equal(stockId, payload.StockId);
        Assert.Equal("SKU-1", payload.Sku);
        Assert.Equal(Warehouse, payload.WarehouseId);
        Assert.Equal(SystemActor.Id, payload.OperatorId);  // origin-mesin (no HttpContext) → SYSTEM (ADR-0027)
    }

    // ---- harness ----

    // seed PutawayTask Assigned untuk stock OnHand, kembalikan taskId
    private static async Task<Guid> SeedPutawayTaskAsync(ServiceProvider sp, Guid stockId)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var task = PutawayTask.Assign(PutawayTaskId.New(), new StockId(stockId), "REC-01", "RACK-A1", assignedTo: null);
        db.PutawayTasks.Add(task);
        await db.SaveChangesAsync();
        return task.Id.Value;
    }

    private async Task<ServiceProvider> BuildInventoryAsync()
    {
        var sp = new ServiceCollection()
            .AddLogging()
            .AddInventoryInfrastructure(await fixture.CreateDatabaseAsync())
            .AddInventoryLocationCatalogStub()
            .AddLocalMessaging()
            .BuildServiceProvider();

        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.EnsureCreatedAsync();
        return sp;
    }

    // seed Stock pada state tertentu via domain factory+transisi (lalu persist), kembalikan id.
    private static async Task<Guid> SeedAsync(
        ServiceProvider sp, StockStatus target, string sku, string? batch, DateOnly? expiry, int qty, Guid? waveId = null)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var stock = Stock.CreateOnHand(StockId.New(), Warehouse, sku, "REC-01", batch, expiry, qty, Guid.NewGuid()).Value;
        if (target is StockStatus.Available or StockStatus.Allocated or StockStatus.Picked)
            Assert.True(stock.Putaway("RACK-A1").IsSuccess);
        if (target is StockStatus.Allocated or StockStatus.Picked)
            Assert.True(stock.Allocate(waveId ?? Guid.NewGuid()).IsSuccess);
        if (target is StockStatus.Picked)
            Assert.True(stock.Pick(Guid.NewGuid(), "STG-1").IsSuccess);

        db.Stocks.Add(stock);
        await db.SaveChangesAsync();
        return stock.Id.Value;
    }

    private static async Task DeliverGrConfirmedAsync(ServiceProvider sp, Guid eventId, GRConfirmedV1 message)
    {
        using var scope = sp.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<GoodsReceiptConfirmedConsumer>().HandleAsync(eventId, message);
        AssertSuccess(result);
    }

    private static async Task DeliverWaveReleasedAsync(ServiceProvider sp, Guid eventId, WaveReleasedV1 message)
    {
        using var scope = sp.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<WaveReleasedConsumer>().HandleAsync(eventId, message);
        AssertSuccess(result);
    }

    private static async Task DeliverPickingCompletedAsync(ServiceProvider sp, Guid eventId, PickingCompletedV1 message)
    {
        using var scope = sp.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<PickingCompletedConsumer>().HandleAsync(eventId, message);
        AssertSuccess(result);
    }

    private static async Task DeliverShipmentDispatchedAsync(ServiceProvider sp, Guid eventId, ShipmentDispatchedV1 message)
    {
        using var scope = sp.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<ShipmentDispatchedConsumer>().HandleAsync(eventId, message);
        AssertSuccess(result);
    }

    private static void AssertSuccess(Result result) =>
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
}
