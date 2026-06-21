using Microsoft.EntityFrameworkCore;
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
        base.OnModelCreating(modelBuilder);
    }
}
