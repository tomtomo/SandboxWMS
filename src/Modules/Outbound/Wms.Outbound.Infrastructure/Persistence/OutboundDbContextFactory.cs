using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: design-time DbContext factory (EF tooling)
// Why: `dotnet ef migrations` perlu meng-instansiasi OutboundDbContext tanpa host runtime. Connection
// string placeholder — `migrations add` hanya membaca model.
// How: bangun options persis seperti runtime (UseNpgsql + snake_case) agar model scaffold identik produksi.
public sealed class OutboundDbContextFactory : IDesignTimeDbContextFactory<OutboundDbContext>
{
    public OutboundDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OutboundDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=outbounddb;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", OutboundDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new OutboundDbContext(options);
    }
}
