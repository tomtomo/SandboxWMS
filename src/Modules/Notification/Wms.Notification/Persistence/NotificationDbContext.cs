using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Domain.Primitives;
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

        // Optimistic concurrency (ADR-0031): xmin (PostgreSQL system column, zero-schema-cost) sebagai
        // concurrency token di TIAP aggregate root — konvensi (bukan per-config) → root kini & nanti otomatis
        // ter-proteksi, tak luput; owned/child & tabel rail (non-AggregateRoot) di-skip.
        // UseXminAsConcurrencyToken deprecated tapi DIPAKAI SENGAJA: ia migration-safe (Npgsql exclude
        // system-column xmin dari migration). Replacement manual Property<uint>("xmin") berisiko `migrations
        // add` emit AddColumn xmin yang gagal di-apply. Revisit bila Npgsql menghapus API (upgrade major).
#pragma warning disable CS0618
        foreach (var rootType in modelBuilder.Model.GetEntityTypes()
                     .Select(entity => entity.ClrType)
                     .Where(type => type.DerivesFromAggregateRoot())
                     .ToList())
            modelBuilder.Entity(rootType).UseXminAsConcurrencyToken();
#pragma warning restore CS0618
        base.OnModelCreating(modelBuilder);
    }
}
