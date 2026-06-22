using Wms.BuildingBlocks.Domain.Events;

namespace Wms.Inbound.Domain;

// What: Domain Event (DDD; emission policy ADR-0026)
// Why: menandai fakta bisnis "GR dikonfirmasi" di dalam aggregate, in-process. Membawa hasil
// TURUNAN resolusi two-axis (ADR-0013): receivedLines (Good/QcHold → masuk Stock) dan rejectedLines
// (WrongItem/excess → tidak). Diterjemahkan jadi integration event GRConfirmedV1 di Application
// sebelum menyeberang broker (ADR-0005) — tipe domain ini tak pernah jadi wire-contract.
// SupplierId (nullable): data domain header GR yang di-bawa untuk Reporting ReceivingSummary
// per-supplier (ADR-0030) — supplier adalah atribut bisnis aggregate, jadi mengalir lewat event ini
// (beda dgn operator/aktor yang concern audit → di-source ICurrentUser di handler, bukan domain).
public sealed record GoodsReceiptConfirmed(
    GoodsReceiptId GoodsReceiptId,
    string WarehouseId,
    string? SupplierId,
    IReadOnlyList<ConfirmedReceivedLine> ReceivedLines,
    IReadOnlyList<ConfirmedRejectedLine> RejectedLines) : IDomainEvent;

// What: line yang diterima masuk inventory (in-process snapshot) — Good→OnHand, QcHold→Quarantine
public sealed record ConfirmedReceivedLine(
    string Sku, int Quantity, LineStatus Status, string? Batch, DateOnly? Expiry);

// What: line yang ditolak (tak masuk inventory) — return-to-vendor / excess (metadata)
public sealed record ConfirmedRejectedLine(string Sku, int Quantity, RejectionReason Reason);
