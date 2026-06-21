namespace Wms.BuildingBlocks.Application.Auditing;

// What: AuditLogEntry — append-only audit record + port DTO untuk IAuditLogStore (ADR-0022)
// Why: sengaja tinggal di Application (bukan Infrastructure) walau ia tabel forensik — ia
// adalah bahasa yang diucapkan port IAuditLogStore, dan adapter Platform.Local meng-implement
// port itu tanpa boleh me-reference BuildingBlocks.Infrastructure (FF cross-layer). Mirror pola
// DeadLetterMessage. EF mapping-nya ada di Infrastructure (AddInfrastructureTables); tipe-nya
// tetap POCO bersih tanpa atribut persistence.
// How: class (bukan record) supaya EF change-tracking aman; di-write SEKALI per command teraudit,
// outcome-aware (IsSuccess + ErrorCode dari Result) — termasuk attempt yang GAGAL (inti forensik).
public sealed class AuditLogEntry
{
    public Guid Id { get; init; }

    // pelaku (ICurrentUser.UserId): userId terotentikasi, SYSTEM, atau anonymous
    public string Actor { get; init; } = null!;

    // nama command (mis. "ConfirmGoodsReceiptCommand") — tindakan yang dilakukan
    public string Action { get; init; } = null!;

    // objek yang dikenai (IAuditableCommand) — korelasi forensik per-aggregate
    public string AggregateType { get; init; } = null!;

    public string AggregateId { get; init; } = null!;

    // outcome-aware (ADR-0022): true=sukses; false=ditolak/gagal (attempt tetap terekam)
    public bool IsSuccess { get; init; }

    // Error.Code dari Result saat gagal (null saat sukses) — sumbu forensik "kenapa ditolak"
    public string? ErrorCode { get; init; }

    // payload command yang SUDAH di-redaksi PII (AuditRedaction) — konteks, bukan rahasia
    public string? Payload { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    // W3C traceparent (Activity.Current) — jembatan audit ⇄ distributed trace (ADR-0024 baseline)
    public string? Traceparent { get; init; }
}
