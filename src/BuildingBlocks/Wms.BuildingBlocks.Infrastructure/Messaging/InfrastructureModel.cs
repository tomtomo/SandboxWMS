using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: shared infra-table mapping (ADR-0010 amendment — infra-table ownership)
// Why: outbox/inbox/dead_letter/audit_log dimiliki & dimigrasi oleh DbContext TIAP modul,
// dipetakan lewat extension bersama ini ke schema "infrastructure" di dalam DB
// per-service. "InfrastructureDbContext standalone DILARANG" (cegah kontaminasi PK
// lintas-service, FF#10) — jadi tak ada DbContext sendiri; modul memanggilnya di
// OnModelCreating. Satu definisi tabel, dipakai ulang semua modul.
// How: extension di ModelBuilder; penamaan kolom snake_case dihasilkan oleh
// UseSnakeCaseNamingConvention di sisi provider (UseNpgsql), jadi di sini bersih
// dari HasColumnName.
public static class InfrastructureModel
{
    public const string Schema = "infrastructure";

    public static ModelBuilder AddInfrastructureTables(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox", Schema);
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();          // Id = envelope.EventId (app-assigned)
            b.Property(x => x.LogicalName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Payload).IsRequired();
            b.Property(x => x.Traceparent).HasMaxLength(64);
            b.Property(x => x.Tracestate).HasMaxLength(512);
            b.Property(x => x.LastError).HasMaxLength(2048);
            // index pendukung query "unsent": baris belum diproses, urut waktu kejadian
            b.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox", Schema);
            // composite PK (event_id, handler_type) — multi-consumer safe (ADR-0005)
            b.HasKey(x => new { x.EventId, x.HandlerType });
            b.Property(x => x.HandlerType).HasMaxLength(200);
        });

        modelBuilder.Entity<DeadLetterMessage>(b =>
        {
            b.ToTable("dead_letter", Schema);
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.LogicalName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Payload).IsRequired();
            b.Property(x => x.Source).HasMaxLength(200).IsRequired();
            b.Property(x => x.Error).HasMaxLength(4096).IsRequired();
            b.Property(x => x.Traceparent).HasMaxLength(64);
            b.Property(x => x.Tracestate).HasMaxLength(512);
            b.HasIndex(x => x.EventId);                           // korelasi forensik per event
        });

        // What: tabel audit-log operasional (ADR-0022) — append-only, outcome-aware
        // Why: ko-lokasi di schema "infrastructure" DB per-service, sama spt dead_letter —
        // ditulis OUT-OF-BAND (koneksi sendiri) oleh AuditLogBehavior, survive rollback bisnis.
        modelBuilder.Entity<AuditLogEntry>(b =>
        {
            b.ToTable("audit_log", Schema);
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();          // Id app-assigned (per command)
            b.Property(x => x.Actor).HasMaxLength(200).IsRequired();
            b.Property(x => x.Action).HasMaxLength(200).IsRequired();
            b.Property(x => x.AggregateType).HasMaxLength(200).IsRequired();
            b.Property(x => x.AggregateId).HasMaxLength(200).IsRequired();
            b.Property(x => x.ErrorCode).HasMaxLength(200);
            b.Property(x => x.Traceparent).HasMaxLength(64);
            // index pendukung query forensik "timeline per-aggregate", urut waktu kejadian
            b.HasIndex(x => new { x.AggregateType, x.AggregateId, x.OccurredAt });
        });

        return modelBuilder;
    }
}
