namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: Transactional Outbox record (EIP — Guaranteed Delivery; ADR-0005)
// Why: inti anti dual-write. Baris ini ditulis dalam transaksi yang SAMA dengan
// state bisnis, lalu di-relay async oleh OutboxDispatcher — sehingga "kalau state
// berubah, event PASTI akhirnya terkirim" tanpa distributed transaction. Tipe ini
// internal-mechanism (bukan port DTO) → tinggal di Infrastructure.
// How: class (EF change-tracking aman); Id = envelope.EventId (satu baris per event).
// ProcessedAt null = belum terkirim; Attempts naik tiap kegagalan publish; saat
// melewati batas → dipindah ke dead_letter dan ditandai processed (stop retry).
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    public string LogicalName { get; init; } = null!;

    public string Payload { get; init; } = null!;

    public DateTimeOffset OccurredAt { get; init; }

    // W3C trace-context snapshot saat di-enqueue (ADR-0024) — dibawa lintas hop broker
    public string? Traceparent { get; init; }

    public string? Tracestate { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
