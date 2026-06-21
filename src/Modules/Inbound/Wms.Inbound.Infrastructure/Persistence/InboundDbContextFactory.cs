using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wms.Inbound.Infrastructure.Persistence;

// What: design-time DbContext factory (EF tooling)
// Why: `dotnet ef migrations` perlu meng-instansiasi InboundDbContext tanpa host
// runtime. Connection string di sini placeholder — `migrations add` hanya membaca
// model, tak konek DB (apply sungguhan lewat MigrationRunner dgn config nyata).
// How: bangun DbContextOptions persis seperti runtime (UseNpgsql + snake_case) supaya
// model yang di-scaffold identik dengan yang dipakai produksi.
public sealed class InboundDbContextFactory : IDesignTimeDbContextFactory<InboundDbContext>
{
    public InboundDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InboundDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=inbounddb;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", InboundDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new InboundDbContext(options);
    }
}
