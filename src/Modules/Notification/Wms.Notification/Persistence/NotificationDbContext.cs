using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Notification.Domain;

namespace Wms.Notification.Persistence;

// What: DbContext modul Notification (DDD Unit of Work; ADR-0010 DB-per-service, ADR-0017 consumer)
// Why: Notification memiliki datanya sendiri — schema "notification" (subscription + delivery aggregate)
// + tabel rail "infrastructure" (inbox dedup + dead_letter forensik) ko-lokasi. Outbox/audit_log idle
// (pure consumer tak emit, audit di-skip) — sama pola Reporting. Beda dari Reporting: Notification PUNYA
// write-model aggregate (delivery state machine), bukan projection — tapi tetap collapsed 1 project.
// How: HasDefaultSchema("notification"); AddInfrastructureTables memetakan rail; konfigurasi aggregate via
// ApplyConfigurationsFromAssembly. snake_case di seam UseNpgsql (host/factory).
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public const string Schema = "notification";

    public DbSet<NotificationSubscription> Subscriptions => Set<NotificationSubscription>();

    public DbSet<NotificationDelivery> Deliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInfrastructureTables();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
