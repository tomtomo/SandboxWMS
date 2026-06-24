using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MediatR;
using Testcontainers.RabbitMq;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inbound.Application.DependencyInjection;
using Wms.Inbound.Application.Features.ConfirmGoodsReceipt;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;
using Wms.Inbound.Application.Features.DeclareScanComplete;
using Wms.Inbound.Application.Features.ScanItem;
using Wms.Inbound.Domain;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Inbound.Infrastructure.Persistence;
using Wms.Inventory.Domain;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Messaging;
using Wms.Inventory.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.Platform.Local.Messaging;
using Wms.TestSupport;

namespace Wms.Inventory.IntegrationTests;

// What: cross-process E2E lewat RabbitMQ NYATA (ADR-0029 amendment) — bukti delivery lintas-"proses"
// Why: WalkingSkeletonChainTests membuktikan choreography Inbound→Inventory tapi via SATU InMemoryMessagePublisher
// berbagi (single-process). Test ini menguji yang dulu IDLE: event yang di-publish OUTBOX provider Inbound
// benar-benar menyeberang BROKER RabbitMQ ke consumer provider Inventory yang TERPISAH (dua IConnection, dua
// "host"). Membuktikan adapter RabbitMqMessagePublisher (publish→exchange) + RabbitMqConsumerHostedService
// (queue durable bind "#" → dispatcher) bekerja end-to-end terhadap broker sungguhan (Testcontainers).
// gRPC MasterData di-stub (orthogonal — lokasi default + uom snapshot), fokus = transport event.
[Collection(PostgresCollection.Name)]
public sealed class RabbitMqChainTests(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.13-management-alpine").Build();

    public Task InitializeAsync() => _rabbit.StartAsync();

    public Task DisposeAsync() => _rabbit.DisposeAsync().AsTask();

    [Fact]
    public async Task GrConfirmed_published_to_outbox_crosses_rabbitmq_to_inventory_consumer()
    {
        var conn = _rabbit.GetConnectionString();

        // ---- producer "host": Inbound + outbox + RabbitMQ publisher (queue tak dipakai — producer only) ----
        await using var inbound = new ServiceCollection()
            .AddLogging()
            .AddInboundApplication()
            .AddInboundProductCatalogStub()
            .AddInboundInfrastructure(await fixture.CreateDatabaseAsync())
            .AddRabbitMqMessaging(conn, "inbound-test")
            .BuildServiceProvider();

        // ---- consumer "host": Inventory + RabbitMQ consumer (queue durable bind "#") ----
        await using var inventory = new ServiceCollection()
            .AddLogging()
            .AddInventoryInfrastructure(await fixture.CreateDatabaseAsync())
            .AddInventoryLocationCatalogStub()
            .AddRabbitMqMessaging(conn, "inventory-test")
            .BuildServiceProvider();

        await CreateSchemaAsync<InboundDbContext>(inbound);
        await CreateSchemaAsync<InventoryDbContext>(inventory);

        // subscribe dispatcher Inventory ke rail RabbitMQ, LALU start hosted consumer (declare+bind queue
        // SEBELUM producer publish — topic exchange men-drop pesan tak ter-route bila queue belum ada).
        var subscriber = inventory.GetRequiredService<IMessageSubscriber>();
        var dispatcher = inventory.GetRequiredService<InventoryIntegrationEventDispatcher>();
        subscriber.Subscribe(dispatcher.HandleGoodsReceiptConfirmedAsync);
        var consumerService = inventory.GetServices<IHostedService>().OfType<RabbitMqConsumerHostedService>().Single();
        await consumerService.StartAsync(CancellationToken.None);

        // ---- drive Inbound: GR create → scan → complete → confirm (tulis GRConfirmed ke outbox) ----
        var grId = await CreateGoodsReceiptAsync(inbound, "WH-JKT", ("SKU-1", 10), ("SKU-2", 5));
        await ConfirmGoodsReceiptAsync(inbound, grId);

        // ---- outbox relay → publish ke RabbitMQ (publisher confirm: throw bila broker tak ack) ----
        var dispatched = await DispatchOutboxAsync(inbound);
        Assert.Equal(1, dispatched);

        // ---- CROSS-PROCESS: consumer Inventory menerima dari queue async → Stock + PutawayTask ----
        var stocks = await PollAsync(inventory, expected: 2, timeout: TimeSpan.FromSeconds(20));
        Assert.Equal(2, stocks);

        var state = await InventoryStateAsync(inventory);
        Assert.Equal(2, state.PutawayTasks);
        Assert.Equal(1, state.InboxRows);
        Assert.All(state.StockStatuses, status => Assert.Equal(StockStatus.OnHand, status));

        await consumerService.StopAsync(CancellationToken.None);
    }

    private static async Task<Guid> CreateGoodsReceiptAsync(
        IServiceProvider inbound, string warehouseId, params (string Sku, int Qty)[] lines)
    {
        using var scope = inbound.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var create = await sender.Send(new CreateGoodsReceiptCommand(
            warehouseId, [.. lines.Select(l => new CreateGoodsReceiptLine(l.Sku, l.Qty))]));
        Assert.True(create.IsSuccess);
        var grId = create.Value;

        foreach (var line in lines)
            Assert.True((await sender.Send(new ScanItemCommand(
                grId, line.Sku, line.Qty, null, null, LineStatus.Good))).IsSuccess);

        Assert.True((await sender.Send(new DeclareScanCompleteCommand(grId))).IsSuccess);
        return grId;
    }

    private static async Task ConfirmGoodsReceiptAsync(IServiceProvider inbound, Guid grId)
    {
        using var scope = inbound.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Assert.True((await sender.Send(new ConfirmGoodsReceiptCommand(grId))).IsSuccess);
    }

    private static async Task<int> DispatchOutboxAsync(IServiceProvider inbound)
    {
        using var scope = inbound.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InboundDbContext>();
        var publisher = inbound.GetRequiredService<IMessagePublisher>();   // RabbitMqMessagePublisher (singleton)
        var dispatcher = new OutboxDispatcher(
            new UnusedScopeFactory(), new OutboxOptions(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OutboxDispatcher>.Instance);
        return await dispatcher.ProcessOnceAsync(db, publisher, new LocalDeadLetterStore(db));
    }

    private static async Task<int> PollAsync(IServiceProvider inventory, int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = inventory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var count = await db.Stocks.CountAsync();
            if (count >= expected)
                return count;
            await Task.Delay(500);
        }
        using var last = inventory.CreateScope();
        return await last.ServiceProvider.GetRequiredService<InventoryDbContext>().Stocks.CountAsync();
    }

    private static async Task<InventorySnapshot> InventoryStateAsync(IServiceProvider inventory)
    {
        using var scope = inventory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return new InventorySnapshot(
            await db.PutawayTasks.CountAsync(),
            await db.Set<InboxMessage>().CountAsync(),
            await db.Stocks.Select(s => s.Status).ToListAsync());
    }

    private static async Task CreateSchemaAsync<TContext>(IServiceProvider provider)
        where TContext : DbContext
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreatedAsync();
    }

    private sealed record InventorySnapshot(int PutawayTasks, int InboxRows, IReadOnlyList<StockStatus> StockStatuses);

    // ProcessOnceAsync tak menyentuh scope factory (hanya untuk loop BackgroundService)
    private sealed class UnusedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException();
    }
}
