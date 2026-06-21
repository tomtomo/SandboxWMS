using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inventory.Domain;
using Wms.Inventory.Infrastructure.Persistence;
using Wms.TestSupport;

namespace Wms.Inventory.IntegrationTests;

// What: integration test migration InventoryDbContext (DoD Phase 03b)
// Why: memastikan migration NYATA (yang sama dipakai MigrationRunner) — termasuk
// AddStockLifecycleAndPutawayCompletion — provisioning schema "inventory" + rail "infrastructure"
// pada Postgres sungguhan, BUKAN cuma model EnsureCreated. Round-trip kolom lifecycle baru
// (location/batch/expiry/allocated_to_wave_id) membuktikan migration ↔ model sinkron.
[Collection(PostgresCollection.Name)]
public sealed class InfrastructureMigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task InventoryDbContext_migrations_provision_schema_and_lifecycle_columns()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(await fixture.CreateDatabaseAsync(), npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", InventoryDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new InventoryDbContext(options);
        await db.Database.MigrateAsync();

        // aggregate + rail tables ter-provision → query tak melempar
        Assert.Equal(0, await db.Stocks.CountAsync());
        Assert.Equal(0, await db.PutawayTasks.CountAsync());
        Assert.Equal(0, await db.Set<OutboxMessage>().CountAsync());
        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());

        // migration lifecycle 03b ter-apply
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, name => name.EndsWith("AddStockLifecycleAndPutawayCompletion"));

        // round-trip kolom lifecycle baru lewat schema TERMIGRASI: write transisi penuh, baca balik
        var stock = Stock.CreateOnHand(
            StockId.New(), "WH-JKT", "SKU-1", "REC-01", "B1", new DateOnly(2026, 12, 31), 10, Guid.NewGuid()).Value;
        stock.Putaway("RACK-A1");
        var waveId = Guid.NewGuid();
        stock.Allocate(waveId);
        db.Stocks.Add(stock);
        await db.SaveChangesAsync();

        var persisted = await db.Stocks.SingleAsync();
        Assert.Equal(StockStatus.Allocated, persisted.Status);
        Assert.Equal("RACK-A1", persisted.LocationId);
        Assert.Equal("B1", persisted.Batch);
        Assert.Equal(new DateOnly(2026, 12, 31), persisted.Expiry);
        Assert.Equal(waveId, persisted.AllocatedToWaveId);
    }
}
