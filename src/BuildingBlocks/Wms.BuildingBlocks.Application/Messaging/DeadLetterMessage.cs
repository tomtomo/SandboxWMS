namespace Wms.BuildingBlocks.Application.Messaging;

// What: Dead Letter record (EIP — Dead Letter Channel) + port DTO untuk IDeadLetterStore
// Why: sengaja tinggal di Application (bukan Infrastructure) walau ia tabel forensik —
// karena ia adalah bahasa yang diucapkan port IDeadLetterStore, dan adapter
// Platform.Local meng-implement port itu tanpa boleh me-reference BuildingBlocks
// .Infrastructure. EF mapping-nya ada di Infrastructure (AddInfrastructureTables);
// tipe-nya tetap POCO bersih tanpa atribut persistence.
// How: class (bukan record) supaya EF change-tracking aman; di-Add saat sebuah pesan
// melewati batas retry (poison message) untuk inspeksi manual + kemungkinan replay.
public sealed class DeadLetterMessage
{
    public Guid Id { get; init; }

    // identitas logical event yang gagal (envelope.EventId) — kunci korelasi forensik
    public Guid EventId { get; init; }

    public string LogicalName { get; init; } = null!;

    public string Payload { get; init; } = null!;

    // asal kegagalan, mis. "outbox-dispatch" (produser) atau nama handler (konsumer)
    public string Source { get; init; } = null!;

    public string Error { get; init; } = null!;

    public int Attempts { get; init; }

    public DateTimeOffset DeadLetteredAt { get; init; }

    public string? Traceparent { get; init; }

    public string? Tracestate { get; init; }
}
