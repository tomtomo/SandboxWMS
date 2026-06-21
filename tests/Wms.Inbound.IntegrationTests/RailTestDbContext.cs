using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Infrastructure.Messaging;

namespace Wms.Inbound.IntegrationTests;

// What: DbContext harness untuk menguji RAIL (bukan context produksi)
// Why: rail (outbox/inbox/dead_letter) adalah deliverable 01b; harness ini memetakan
// tabel rail via AddInfrastructureTables YANG SAMA dipakai produksi + satu tabel efek
// bisnis (HandledEvent) sebagai bukti idempotency — tanpa mencemari InboundDbContext
// dengan tabel test. InboundDbContext + migration-nya diuji terpisah.
// How: OnModelCreating panggil AddInfrastructureTables + map HandledEvent di schema "test".
public sealed class RailTestDbContext(DbContextOptions<RailTestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddInfrastructureTables();

        modelBuilder.Entity<HandledEvent>(b =>
        {
            b.ToTable("handled_event", "test");
            b.HasKey(x => x.Id);
        });

        base.OnModelCreating(modelBuilder);
    }
}

// efek bisnis konsumer: satu baris per event yang berhasil diproses (bukti dedup)
public sealed class HandledEvent
{
    public Guid Id { get; init; }

    public Guid EventId { get; init; }

    public string LogicalName { get; init; } = null!;
}
