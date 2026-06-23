using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Infrastructure.Persistence;

// What: DbContext modul Inbound (DDD Unit of Work; ADR-0010 DB-per-service)
// Why: tiap modul memiliki datanya sendiri — DbContext ini menyentuh schema "inbound"
// (aggregate modul, menyusul 01c/03a) + tabel rail "infrastructure" (outbox/inbox/
// dead_letter) yang ko-lokasi di DB service ini. Tak ada DbContext lintas-service; tak
// ada InfrastructureDbContext standalone (FF#10) — rail "menumpang" di context modul.
// How: HasDefaultSchema("inbound") untuk tabel modul; AddInfrastructureTables memetakan
// rail ke schema "infrastructure". snake_case diterapkan di seam UseNpgsql (DI/factory).
public sealed class InboundDbContext(DbContextOptions<InboundDbContext> options) : DbContext(options)
{
    public const string Schema = "inbound";

    // What: DbSet aggregate root GoodsReceipt (write-model modul Inbound)
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

    // What: DbSet aggregate root GRAttachment (terpisah, ADR-0015) — logical FK ke GoodsReceipt
    public DbSet<GRAttachment> Attachments => Set<GRAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInfrastructureTables();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InboundDbContext).Assembly);

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
