using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inbound.Application.DependencyInjection;
using Wms.Inbound.Application.Features.ConfirmGoodsReceipt;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;
using Wms.Inbound.Application.Features.DeclareScanComplete;
using Wms.Inbound.Application.Features.ScanItem;
using Wms.Inbound.Contracts;
using Wms.Inbound.Domain;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Inbound.Infrastructure.Persistence;
using Wms.Inventory.Domain;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Messaging;
using Wms.Inventory.Infrastructure.Persistence;
using Wms.Platform.Local.Messaging;
using Wms.TestSupport;

namespace Wms.Inventory.IntegrationTests;

// What: walking-skeleton E2E (Cockburn) — DoD Phase 01c (Opsi C / ADR-0029)
// Why: membuktikan choreography lintas-context Inbound→Inventory hidup dalam SATU proses —
// dua ServiceProvider (dua "service") berbagi satu InMemoryMessagePublisher sebagai
// stand-in broker. Chain: GR confirm → GRConfirmed via Outbox → dispatch → consumer
// (Inbox dedup) → Stock(OnHand) + PutawayTask(Assigned). Plus idempotency: redelivery
// at-least-once → efek hanya sekali.
[Collection(PostgresCollection.Name)]
public sealed class WalkingSkeletonChainTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Confirming_goods_receipt_creates_stock_and_putaway_in_inventory()
    {
        await using var chain = await EventChain.CreateAsync(fixture);

        var goodsReceiptId = await chain.CreateGoodsReceiptAsync("WH-JKT", ("SKU-1", 10), ("SKU-2", 5));
        await chain.ConfirmGoodsReceiptAsync(goodsReceiptId);

        var dispatched = await chain.DispatchOutboxAsync();

        Assert.Equal(1, dispatched);                                  // satu integration event ter-publish
        var state = await chain.InventoryStateAsync();
        Assert.Equal(2, state.Stocks);                                // dua receivedLine → dua Stock
        Assert.Equal(2, state.PutawayTasks);                          // satu PutawayTask per Stock OnHand
        Assert.Equal(1, state.InboxRows);                             // satu event diproses
        Assert.All(state.StockStatuses, status => Assert.Equal(StockStatus.OnHand, status));
        Assert.All(state.PutawayStatuses, status => Assert.Equal(PutawayTaskStatus.Assigned, status));
    }

    [Fact]
    public async Task Duplicate_gr_confirmed_delivery_creates_stock_once()
    {
        await using var chain = await EventChain.CreateAsync(fixture);

        var envelope = EventChain.GRConfirmedEnvelope("WH-JKT", ("SKU-1", 10));

        await chain.DeliverAsync(envelope);
        await chain.DeliverAsync(envelope);                           // redelivery (at-least-once)

        var state = await chain.InventoryStateAsync();
        Assert.Equal(1, state.Stocks);                               // efek hanya sekali (Inbox dedup)
        Assert.Equal(1, state.PutawayTasks);
        Assert.Equal(1, state.InboxRows);
    }

    // What: harness E2E — dua "service" (provider) + broker in-proc bersama
    // How: inboundProvider = produser; inventoryProvider = konsumen; publisher di-Subscribe
    // ke dispatcher Inventory. DB-per-service: dua database Postgres terpisah (EnsureCreated).
    private sealed class EventChain : IAsyncDisposable
    {
        private readonly ServiceProvider _inbound;
        private readonly ServiceProvider _inventory;
        private readonly InMemoryMessagePublisher _publisher;

        private EventChain(ServiceProvider inbound, ServiceProvider inventory, InMemoryMessagePublisher publisher)
        {
            _inbound = inbound;
            _inventory = inventory;
            _publisher = publisher;
        }

        public static async Task<EventChain> CreateAsync(PostgresFixture fixture)
        {
            var inbound = new ServiceCollection()
                .AddLogging()
                .AddInboundApplication()
                .AddInboundProductCatalogStub()
                .AddInboundInfrastructure(await fixture.CreateDatabaseAsync())
                .BuildServiceProvider();

            var inventory = new ServiceCollection()
                .AddLogging()
                .AddInventoryInfrastructure(await fixture.CreateDatabaseAsync())
                .AddInventoryLocationCatalogStub()
                .BuildServiceProvider();

            // broker stand-in in-proc: producer publish → subscriber consumer (co-located di test)
            var publisher = new InMemoryMessagePublisher(NullLogger<InMemoryMessagePublisher>.Instance);
            var dispatcher = inventory.GetRequiredService<InventoryIntegrationEventDispatcher>();
            publisher.Subscribe(dispatcher.HandleGoodsReceiptConfirmedAsync);

            await CreateSchemaAsync<InboundDbContext>(inbound);
            await CreateSchemaAsync<InventoryDbContext>(inventory);

            return new EventChain(inbound, inventory, publisher);
        }

        // flow penuh state machine 03a: create (expectedLines) → scan tiap line Good (qty=expected,
        // tak ada discrepancy) → declare complete → Pending, siap di-Confirm.
        public async Task<Guid> CreateGoodsReceiptAsync(string warehouseId, params (string Sku, int Qty)[] lines)
        {
            using var scope = _inbound.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var create = await sender.Send(new CreateGoodsReceiptCommand(
                warehouseId, [.. lines.Select(line => new CreateGoodsReceiptLine(line.Sku, line.Qty))]));
            Assert.True(create.IsSuccess);
            var goodsReceiptId = create.Value;

            foreach (var line in lines)
            {
                var scan = await sender.Send(new ScanItemCommand(
                    goodsReceiptId, line.Sku, line.Qty, null, null, LineStatus.Good));
                Assert.True(scan.IsSuccess);
            }

            Assert.True((await sender.Send(new DeclareScanCompleteCommand(goodsReceiptId))).IsSuccess);
            return goodsReceiptId;
        }

        public async Task ConfirmGoodsReceiptAsync(Guid goodsReceiptId)
        {
            using var scope = _inbound.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new ConfirmGoodsReceiptCommand(goodsReceiptId));
            Assert.True(result.IsSuccess);
        }

        // satu pass Outbox relay → publish ke broker stand-in → consumer jalan sinkron
        public async Task<int> DispatchOutboxAsync()
        {
            using var scope = _inbound.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InboundDbContext>();
            var dispatcher = new OutboxDispatcher(
                new UnusedScopeFactory(), new OutboxOptions(), NullLogger<OutboxDispatcher>.Instance);
            return await dispatcher.ProcessOnceAsync(db, _publisher, new LocalDeadLetterStore(db));
        }

        public Task DeliverAsync(MessageEnvelope envelope) => _publisher.PublishAsync(envelope);

        public async Task<InventorySnapshot> InventoryStateAsync()
        {
            using var scope = _inventory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            return new InventorySnapshot(
                await db.Stocks.CountAsync(),
                await db.PutawayTasks.CountAsync(),
                await db.Set<InboxMessage>().CountAsync(),
                await db.Stocks.Select(s => s.Status).ToListAsync(),
                await db.PutawayTasks.Select(t => t.Status).ToListAsync());
        }

        public static MessageEnvelope GRConfirmedEnvelope(string warehouseId, params (string Sku, int Qty)[] lines)
        {
            var payload = new GRConfirmedV1(
                Guid.NewGuid(), warehouseId,
                [.. lines.Select(line => new ReceivedLineV1(line.Sku, line.Qty, "Good", null, null))],
                []);

            return new MessageEnvelope(
                EventId: Guid.NewGuid(),
                LogicalName: GRConfirmedV1.LogicalName,
                OccurredAt: DateTimeOffset.UtcNow,
                Payload: JsonSerializer.Serialize(payload),
                Traceparent: null,
                Tracestate: null);
        }

        private static async Task CreateSchemaAsync<TContext>(IServiceProvider provider)
            where TContext : DbContext
        {
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreatedAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _inbound.DisposeAsync();
            await _inventory.DisposeAsync();
        }

        // ProcessOnceAsync tak menyentuh scope factory (itu hanya untuk loop BackgroundService)
        private sealed class UnusedScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope() => throw new NotSupportedException();
        }
    }

    private sealed record InventorySnapshot(
        int Stocks,
        int PutawayTasks,
        int InboxRows,
        IReadOnlyList<StockStatus> StockStatuses,
        IReadOnlyList<PutawayTaskStatus> PutawayStatuses);
}
