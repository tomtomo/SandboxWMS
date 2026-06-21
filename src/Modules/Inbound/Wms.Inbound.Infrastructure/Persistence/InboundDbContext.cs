using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInfrastructureTables();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InboundDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
