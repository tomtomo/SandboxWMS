using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence;

// What: DbContext modul Inventory (DDD Unit of Work; ADR-0010 DB-per-service)
// Why: Inventory memiliki datanya sendiri — schema "inventory" (Stock, PutawayTask) +
// tabel rail "infrastructure" (outbox/inbox/dead_letter) yang ko-lokasi di DB service ini.
// Inbox dipakai consumer untuk idempotency; outbox idle di 01c (Inventory belum emit).
// How: HasDefaultSchema("inventory"); AddInfrastructureTables memetakan rail; konfigurasi
// aggregate via ApplyConfigurationsFromAssembly. snake_case di seam UseNpgsql.
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public const string Schema = "inventory";

    // What: DbSet aggregate roots modul Inventory (write-model)
    public DbSet<Stock> Stocks => Set<Stock>();

    public DbSet<PutawayTask> PutawayTasks => Set<PutawayTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInfrastructureTables();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);

        // Optimistic concurrency (ADR-0031): xmin (PostgreSQL system column, zero-schema-cost) sebagai
        // concurrency token di TIAP aggregate root — konvensi (bukan per-config) → root kini & nanti otomatis
        // ter-proteksi, tak luput; owned/child & tabel rail (non-AggregateRoot) di-skip. Tutup lost-update
        // Stock (Putaway/Allocate/Pick konkuren).
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
