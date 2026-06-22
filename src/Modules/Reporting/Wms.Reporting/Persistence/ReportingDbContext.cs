using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Reporting.Projections;

namespace Wms.Reporting.Persistence;

// What: DbContext modul Reporting (DDD Unit of Work; ADR-0010 DB-per-service, ADR-0017 read-side)
// Why: Reporting memiliki datanya sendiri — schema "reporting" (4 projection denormalized) + tabel rail
// "infrastructure" (inbox dedup + dead_letter forensik) ko-lokasi. Outbox/audit_log idle (pure consumer
// tak emit, tak ada command auditable) — sama pola Inventory di 01c (tabel ada, tak dipakai). Projection
// = read-model tanpa invariant (ADR-0017) → tak ada AuditableEntityInterceptor (tak ada IAuditable).
// How: HasDefaultSchema("reporting"); AddInfrastructureTables memetakan rail; konfigurasi projection via
// ApplyConfigurationsFromAssembly. snake_case di seam UseNpgsql (host/factory).
public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public const string Schema = "reporting";

    public DbSet<StockOnHandView> StockOnHandViews => Set<StockOnHandView>();

    public DbSet<ReceivingSummary> ReceivingSummaries => Set<ReceivingSummary>();

    public DbSet<DispatchSummary> DispatchSummaries => Set<DispatchSummary>();

    public DbSet<OperatorActivity> OperatorActivities => Set<OperatorActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInfrastructureTables();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
