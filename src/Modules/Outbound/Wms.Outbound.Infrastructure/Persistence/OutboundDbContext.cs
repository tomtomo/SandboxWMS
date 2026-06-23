using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: DbContext modul Outbound (DDD Unit of Work; ADR-0010 DB-per-service)
// Why: Outbound memiliki datanya sendiri — schema "outbound" (OutboundOrder, Wave, PickingTask) + tabel
// rail "infrastructure" (outbox/inbox/dead_letter/audit_log) yang ko-lokasi di DB service ini. Outbox AKTIF
// (Outbound emit WaveReleased/PickingCompleted/ShipmentDispatched); Inbox dipakai consumer StockAllocated.
// How: HasDefaultSchema("outbound"); AddInfrastructureTables memetakan rail; konfigurasi aggregate via
// ApplyConfigurationsFromAssembly. snake_case di seam UseNpgsql.
public sealed class OutboundDbContext(DbContextOptions<OutboundDbContext> options) : DbContext(options)
{
    public const string Schema = "outbound";

    // What: DbSet aggregate roots modul Outbound (write-model)
    public DbSet<OutboundOrder> OutboundOrders => Set<OutboundOrder>();

    public DbSet<Wave> Waves => Set<Wave>();

    public DbSet<PickingTask> PickingTasks => Set<PickingTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInfrastructureTables();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OutboundDbContext).Assembly);

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
