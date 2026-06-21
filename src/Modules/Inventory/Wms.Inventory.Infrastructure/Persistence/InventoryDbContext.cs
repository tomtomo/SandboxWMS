using Microsoft.EntityFrameworkCore;
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
        base.OnModelCreating(modelBuilder);
    }
}
