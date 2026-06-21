using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Outbound.Domain;
using Wms.Outbound.Infrastructure.Persistence;
using Wms.TestSupport;

namespace Wms.Outbound.IntegrationTests;

// What: integration test migration OutboundDbContext (DoD Phase 03c)
// Why: memastikan migration NYATA (yang sama dipakai MigrationRunner) — InitialOutbound — provisioning
// schema "outbound" (OutboundOrder/Wave/PickingTask + owned order_lines) + rail "infrastructure" pada
// Postgres sungguhan, BUKAN cuma model EnsureCreated. Round-trip kolom primitive-collection uuid[]
// (order_ids/picking_task_ids) + owned collection (order_lines) membuktikan migration ↔ model sinkron.
[Collection(PostgresCollection.Name)]
public sealed class InfrastructureMigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task OutboundDbContext_migrations_provision_schema_and_collections()
    {
        var options = new DbContextOptionsBuilder<OutboundDbContext>()
            .UseNpgsql(await fixture.CreateDatabaseAsync(), npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", OutboundDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new OutboundDbContext(options);
        await db.Database.MigrateAsync();

        // aggregate + rail tables ter-provision → query tak melempar
        Assert.Equal(0, await db.OutboundOrders.CountAsync());
        Assert.Equal(0, await db.Waves.CountAsync());
        Assert.Equal(0, await db.PickingTasks.CountAsync());
        Assert.Equal(0, await db.Set<OutboxMessage>().CountAsync());
        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, name => name.EndsWith("InitialOutbound"));

        // round-trip OutboundOrder + owned order_lines lewat schema TERMIGRASI
        var order = OutboundOrder.Create(
            OutboundOrderId.New(), "CUST-1", "Jl. Merdeka 1",
            [new OrderLineInput("SKU-1", 10, "carton"), new OrderLineInput("SKU-2", 5, "carton")]).Value;
        db.OutboundOrders.Add(order);

        // round-trip Wave + primitive-collection uuid[] (order_ids/picking_task_ids)
        var taskA = Guid.NewGuid();
        var taskB = Guid.NewGuid();
        var wave = Wave.Activate(WaveId.New(), [order.Id.Value]).Value;
        wave.AttachPickingTasks([taskA, taskB]);
        db.Waves.Add(wave);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var persistedOrder = await db.OutboundOrders.SingleAsync();
        Assert.Equal(2, persistedOrder.OrderLines.Count);
        Assert.Contains(persistedOrder.OrderLines, line => line is { Sku: "SKU-1", Qty: 10, Uom: "carton" });

        var persistedWave = await db.Waves.SingleAsync();
        Assert.Equal(new[] { order.Id.Value }, persistedWave.OrderIds);
        Assert.Equal(new[] { taskA, taskB }, persistedWave.PickingTaskIds);
    }
}
