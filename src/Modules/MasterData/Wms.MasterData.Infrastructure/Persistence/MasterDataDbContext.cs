using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence;

// What: DbContext modul MasterData (DDD Unit of Work; ADR-0010 DB-per-service)
// Why: MasterData memiliki datanya sendiri — schema "masterdata" (Warehouse/Location/Product) +
// tabel rail "infrastructure" (audit_log dipakai utk CRUD writes; outbox/inbox/dead_letter idle —
// reserved untuk event `ProductUpdated` invalidation yang DICATAT-TAK-AKTIF, ADR-0011). Read-API gRPC
// membaca DbContext ini via read-port (reader-delegation), BUKAN gRPC `.Api` langsung (FF#8).
// How: HasDefaultSchema("masterdata"); AddInfrastructureTables memetakan rail; konfigurasi aggregate
// via ApplyConfigurationsFromAssembly; snake_case di seam UseNpgsql.
public sealed class MasterDataDbContext(DbContextOptions<MasterDataDbContext> options) : DbContext(options)
{
    public const string Schema = "masterdata";

    // What: nama global query filter soft-delete (ADR-0014) — anchor dokumentasi targeted-bypass
    public const string SoftDeleteFilter = "masterdata_soft_delete";

    // What: flag bypass soft-delete TER-TARGET (ADR-0014 amendment) — di-set MasterDataReader path inactive
    // Why: EF Core 8 belum punya NAMED query filters (fitur EF 10) → "filter-name-targeted bypass" tak
    // bisa literal. Pola FLAG-GATED di filter tunggal mencapai targeting SETARA: relaks HANYA kondisi
    // isActive (`IncludeInactive || IsActive`), BUKAN blanket IgnoreQueryFilters yang akan mematikan
    // SEMUA filter (termasuk warehouse-scoping kelak, ADR-0012). internal: hanya reader (assembly ini)
    // yang men-togglenya, di-reset try/finally agar tak bocor ke query lain dalam scope.
    internal bool IncludeInactive { get; set; }

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInfrastructureTables();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterDataDbContext).Assembly);

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

        // What: global soft-delete query filter (ADR-0014) — flag-gated untuk targeted bypass.
        // Why: di OnModelCreating (bukan IEntityTypeConfiguration) karena ekspresi mereferensi instance
        // member `IncludeInactive` (DbContext) yang EF parameterisasi & evaluasi ulang per-query.
        modelBuilder.Entity<Warehouse>().HasQueryFilter(w => IncludeInactive || w.IsActive);
        modelBuilder.Entity<Location>().HasQueryFilter(l => IncludeInactive || l.IsActive);
        modelBuilder.Entity<Product>().HasQueryFilter(p => IncludeInactive || p.IsActive);

        base.OnModelCreating(modelBuilder);
    }
}
