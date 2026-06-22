using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wms.Reporting.Persistence;

// What: design-time DbContext factory (EF tooling)
// Why: `dotnet ef migrations` perlu meng-instansiasi ReportingDbContext tanpa host runtime.
// Connection string placeholder — `migrations add` hanya membaca model.
// How: bangun options persis seperti runtime (UseNpgsql + snake_case) agar scaffold identik produksi.
public sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=reportingdb;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", ReportingDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ReportingDbContext(options);
    }
}
