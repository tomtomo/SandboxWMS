namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: Inbox record (EIP — Idempotent Receiver; ADR-0005)
// Why: delivery itu at-least-once (Outbox + broker bisa kirim ulang) → konsumer wajib
// idempotent. Baris ini menandai "(event ini, handler ini) sudah diproses".
// How: PK KOMPOSIT (EventId, HandlerType) — bukan EventId saja. Satu event yang
// fan-out ke banyak handler dalam satu service di-track independen per handler,
// sehingga handler pertama tak memblok sibling-nya (cegah silent loss). MarkProcessed
// di-commit dalam SATU transaksi dengan business write konsumer.
public sealed class InboxMessage
{
    public Guid EventId { get; init; }

    public string HandlerType { get; init; } = null!;

    public DateTimeOffset ProcessedAt { get; init; }
}
