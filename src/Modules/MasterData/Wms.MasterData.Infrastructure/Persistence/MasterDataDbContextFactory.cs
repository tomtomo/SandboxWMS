using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wms.MasterData.Infrastructure.Persistence;

// What: design-time DbContext factory (EF tooling)
// Why: `dotnet ef migrations` perlu meng-instansiasi MasterDataDbContext tanpa host runtime.
// Connection string placeholder — `migrations add` hanya membaca model.
// How: bangun options persis seperti runtime (UseNpgsql + snake_case) agar model scaffold identik.
public sealed class MasterDataDbContextFactory : IDesignTimeDbContextFactory<MasterDataDbContext>
{
    public MasterDataDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MasterDataDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=masterdatadb;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MasterDataDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new MasterDataDbContext(options);
    }
}
