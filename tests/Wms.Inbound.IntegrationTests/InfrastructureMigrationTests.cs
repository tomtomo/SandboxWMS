using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inbound.Infrastructure.Persistence;
using Wms.TestSupport;

namespace Wms.Inbound.IntegrationTests;

// What: integration test migration InboundDbContext (DoD Phase 01b)
// Why: memastikan migration NYATA (yang sama dipakai MigrationRunner) provisioning
// outbox/inbox/dead_letter di schema "infrastructure" pada Postgres sungguhan —
// memvalidasi context produksi + DB-per-service path, bukan cuma model in-memory.
[Collection(PostgresCollection.Name)]
public sealed class InfrastructureMigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task InboundDbContext_migration_provisions_infrastructure_tables()
    {
        var options = new DbContextOptionsBuilder<InboundDbContext>()
            .UseNpgsql(await fixture.CreateDatabaseAsync(), npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", InboundDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new InboundDbContext(options);
        await db.Database.MigrateAsync();

        // tabel rail ter-provision → query tak melempar (Count = 0)
        Assert.Equal(0, await db.Set<OutboxMessage>().CountAsync());
        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());
        Assert.Equal(0, await db.Set<DeadLetterMessage>().CountAsync());

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, name => name.EndsWith("InitialInfrastructure"));
    }
}
