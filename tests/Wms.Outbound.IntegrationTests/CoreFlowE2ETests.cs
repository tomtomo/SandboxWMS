using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inventory.Domain;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Messaging;
using Wms.Inventory.Infrastructure.Persistence;
using Wms.Outbound.Application.DependencyInjection;
using Wms.Outbound.Application.Features.CompletePicking;
using Wms.Outbound.Application.Features.CreateWave;
using Wms.Outbound.Application.Features.DispatchWave;
using Wms.Outbound.Application.Features.ReceiveOutboundOrder;
using Wms.Outbound.Domain;
using Wms.Outbound.Infrastructure.DependencyInjection;
using Wms.Outbound.Infrastructure.Messaging;
using Wms.Outbound.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.Platform.Local.Messaging;
using Wms.TestSupport;

namespace Wms.Outbound.IntegrationTests;

// What: CAPSTONE Phase 03 — full core-flow E2E Inbound→Inventory→Outbound s/d ShipmentDispatched (DoD 03c)
// Why: membuktikan KOREOGRAFI lintas-context Outbound↔Inventory menutup core event chain dalam SATU proses —
// dua ServiceProvider (dua "service" DB-per-service, ADR-0010) berbagi satu InMemoryMessagePublisher sebagai
// stand-in broker. Rantai: OutboundOrder → Wave → WaveReleased → [Inventory alokasi FEFO] → StockAllocated →
// [Outbound PickingTask] → CompletePicking → PickingCompleted → [Inventory Stock Picked] → DispatchWave →
// ShipmentDispatched → [Inventory Stock removed] + OutboundOrder Closed. Tiap hop lewat Outbox→relay→Inbox.
[Collection(PostgresCollection.Name)]
public sealed class CoreFlowE2ETests(PostgresFixture fixture)
{
    private const string Warehouse = "WH-JKT";

    [Fact]
    public async Task Order_to_dispatch_closes_loop_across_outbound_and_inventory()
    {
        await using var chain = await CoreFlow.CreateAsync(fixture);

        // stok Available untuk dua SKU (hasil putaway sebelumnya) — titik awal DoD E2E
        await chain.SeedAvailableStockAsync("SKU-1", "B1");
        await chain.SeedAvailableStockAsync("SKU-2", "B2");

        // 1) dua order eksternal masuk → New
        var orderA = await chain.ReceiveOrderAsync(("SKU-1", 10));
        var orderB = await chain.ReceiveOrderAsync(("SKU-2", 5));

        // 2) SPV buat wave → order InProgress + Wave Active + WaveReleased ke Outbox
        var waveId = await chain.CreateWaveAsync(orderA, orderB);

        // 3) relay WaveReleased → Inventory alokasi FEFO (Available→Allocated) + emit StockAllocated
        Assert.Equal(1, await chain.DrainOutboundAsync());                  // satu WaveReleased ter-publish
        Assert.Equal(2, await chain.InventoryStockCountAsync(StockStatus.Allocated));

        // 4) relay StockAllocated → Outbound buat PickingTask per alokasi (Assigned)
        Assert.Equal(1, await chain.DrainInventoryAsync());                 // satu StockAllocated ter-publish
        var taskIds = await chain.PickingTaskIdsAsync(waveId);
        Assert.Equal(2, taskIds.Count);

        // 5) operator selesaikan picking tiap task → PickingCompleted ke Outbox; saat semua selesai → Wave Ready
        foreach (var taskId in taskIds)
            await chain.CompletePickingAsync(taskId);
        Assert.Equal(WaveStatus.Ready, await chain.WaveStatusAsync(waveId));

        // 6) relay PickingCompleted → Inventory transisi Allocated→Picked
        Assert.Equal(2, await chain.DrainOutboundAsync());                  // dua PickingCompleted ter-publish
        Assert.Equal(2, await chain.InventoryStockCountAsync(StockStatus.Picked));

        // 7) SPV dispatch → Wave Dispatched + order Closed + ShipmentDispatched ke Outbox
        await chain.DispatchWaveAsync(waveId);
        Assert.Equal(WaveStatus.Dispatched, await chain.WaveStatusAsync(waveId));
        Assert.Equal(OutboundOrderStatus.Closed, await chain.OrderStatusAsync(orderA));
        Assert.Equal(OutboundOrderStatus.Closed, await chain.OrderStatusAsync(orderB));

        // 8) relay ShipmentDispatched → Inventory remove Stock Picked → loop tertutup
        Assert.Equal(1, await chain.DrainOutboundAsync());                  // satu ShipmentDispatched ter-publish
        Assert.Equal(0, await chain.InventoryStockCountAsync());            // stok keluar gudang (removed)
    }

    // What: harness E2E — dua "service" (provider) + broker in-proc bersama (mirror WalkingSkeletonChainTests)
    // How: outbound = producer 3 event + consumer StockAllocated; inventory = consumer WaveReleased/
    // PickingCompleted/ShipmentDispatched + producer StockAllocated. publisher (shared) di-Subscribe ke kedua
    // dispatcher; relay = OutboxDispatcher.ProcessOnceAsync menarik outbox tiap provider → publish ke broker.
    private sealed class CoreFlow : IAsyncDisposable
    {
        private readonly ServiceProvider _outbound;
        private readonly ServiceProvider _inventory;
        private readonly InMemoryMessagePublisher _publisher;

        private CoreFlow(ServiceProvider outbound, ServiceProvider inventory, InMemoryMessagePublisher publisher)
        {
            _outbound = outbound;
            _inventory = inventory;
            _publisher = publisher;
        }

        public static async Task<CoreFlow> CreateAsync(PostgresFixture fixture)
        {
            var outbound = new ServiceCollection()
                .AddLogging()
                .AddOutboundApplication()
                .AddOutboundInfrastructure(await fixture.CreateDatabaseAsync())
                .AddLocalAuditing()
                .BuildServiceProvider();

            var inventory = new ServiceCollection()
                .AddLogging()
                .AddInventoryInfrastructure(await fixture.CreateDatabaseAsync())
                .BuildServiceProvider();

            var publisher = new InMemoryMessagePublisher(NullLogger<InMemoryMessagePublisher>.Instance);

            // broker stand-in: tiap dispatcher subscribe event yang relevan; filter LogicalName meng-abaikan
            // envelope yang bukan miliknya (fan-out aman). Outbound consume StockAllocated; Inventory consume 3.
            var inventoryDispatcher = inventory.GetRequiredService<InventoryIntegrationEventDispatcher>();
            var outboundDispatcher = outbound.GetRequiredService<OutboundIntegrationEventDispatcher>();
            publisher.Subscribe(inventoryDispatcher.HandleWaveReleasedAsync);
            publisher.Subscribe(inventoryDispatcher.HandlePickingCompletedAsync);
            publisher.Subscribe(inventoryDispatcher.HandleShipmentDispatchedAsync);
            publisher.Subscribe(outboundDispatcher.HandleStockAllocatedAsync);

            await CreateSchemaAsync<OutboundDbContext>(outbound);
            await CreateSchemaAsync<InventoryDbContext>(inventory);

            return new CoreFlow(outbound, inventory, publisher);
        }

        // seed Stock Available (sudah di-putaway ke rak) untuk SKU — titik awal core flow (DoD E2E)
        public async Task SeedAvailableStockAsync(string sku, string batch)
        {
            using var scope = _inventory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var stock = Stock.CreateOnHand(
                StockId.New(), Warehouse, sku, "REC-01", batch, new DateOnly(2026, 12, 31), 10, Guid.NewGuid()).Value;
            Assert.True(stock.Putaway("RACK-A1").IsSuccess);
            db.Stocks.Add(stock);
            await db.SaveChangesAsync();
        }

        public async Task<Guid> ReceiveOrderAsync(params (string Sku, int Qty)[] lines)
        {
            var command = new ReceiveOutboundOrderCommand(
                "CUST-1", "Jl. Merdeka 1", [.. lines.Select(line => new ReceiveOrderLine(line.Sku, line.Qty))]);
            var result = await SendOutboundAsync(command);
            Assert.True(result.IsSuccess);
            return result.Value;
        }

        public async Task<Guid> CreateWaveAsync(params Guid[] orderIds)
        {
            var result = await SendOutboundAsync(new CreateWaveCommand(orderIds));
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            return result.Value;
        }

        public async Task CompletePickingAsync(Guid pickingTaskId)
        {
            var result = await SendOutboundAsync(new CompletePickingCommand(pickingTaskId, "STG-1"));
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        }

        public async Task DispatchWaveAsync(Guid waveId)
        {
            var result = await SendOutboundAsync(new DispatchWaveCommand(waveId));
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        }

        // satu pass Outbox relay (provider tertentu) → publish ke broker stand-in → consumer jalan sinkron
        public Task<int> DrainOutboundAsync() => DrainAsync(_outbound);

        public Task<int> DrainInventoryAsync() => DrainAsync(_inventory);

        private async Task<int> DrainAsync(ServiceProvider provider)
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var dispatcher = new OutboxDispatcher(
                new UnusedScopeFactory(), new OutboxOptions(), NullLogger<OutboxDispatcher>.Instance);
            return await dispatcher.ProcessOnceAsync(db, _publisher, new LocalDeadLetterStore(db));
        }

        public async Task<int> InventoryStockCountAsync(StockStatus? status = null)
        {
            using var scope = _inventory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            return status is null
                ? await db.Stocks.CountAsync()
                : await db.Stocks.CountAsync(s => s.Status == status);
        }

        public async Task<IReadOnlyList<Guid>> PickingTaskIdsAsync(Guid waveId)
        {
            using var scope = _outbound.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();
            return await db.PickingTasks.Where(t => t.WaveId == waveId).Select(t => t.Id.Value).ToListAsync();
        }

        public async Task<WaveStatus> WaveStatusAsync(Guid waveId)
        {
            using var scope = _outbound.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();
            return (await db.Waves.SingleAsync(w => w.Id == new WaveId(waveId))).Status;
        }

        public async Task<OutboundOrderStatus> OrderStatusAsync(Guid orderId)
        {
            using var scope = _outbound.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OutboundDbContext>();
            return (await db.OutboundOrders.SingleAsync(o => o.Id == new OutboundOrderId(orderId))).Status;
        }

        private async Task<TResponse> SendOutboundAsync<TResponse>(IRequest<TResponse> request)
        {
            using var scope = _outbound.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
        }

        private static async Task CreateSchemaAsync<TContext>(IServiceProvider provider)
            where TContext : DbContext
        {
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreatedAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _outbound.DisposeAsync();
            await _inventory.DisposeAsync();
        }

        // ProcessOnceAsync tak menyentuh scope factory (itu hanya untuk loop BackgroundService)
        private sealed class UnusedScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope() => throw new NotSupportedException();
        }
    }
}
