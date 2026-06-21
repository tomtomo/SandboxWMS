namespace Wms.Inbound.Contracts;

// What: Integration Event (Published Language; ADR-0005 / ADR-0009)
// Why: kontrak publik ber-versi yang menyeberang broker — DECOUPLED dari tipe domain
// GoodsReceiptConfirmed yang in-process. POCO record, ZERO transport/serialization dependency
// (ADR-0009) sehingga konsumen tak terpaksa menarik stack apa pun; aman jadi versioned package
// saat polyrepo (ADR-0007). Status/Reason dibawa sebagai STRING (bukan enum) supaya tetap
// serializer-agnostic — produser/konsumen menerjemahkan di batas (ACL).
// How: record immutable; LogicalName = identitas broker-facing `{module}.{event}.v{N}` (ADR-0023).
//
// What: evolusi non-breaking ke v1 (ADR-0023 SemVer) — Phase 03a memperkaya payload
// Why: tambah `Status` (Good/QcHold) + `Batch`/`Expiry` (nullable) di ReceivedLineV1 dan `RejectedLines`
// baru = penambahan field → tetap v1 (konsumen lama abaikan field baru; field lama tak berubah).
// `Status` dipakai 03b untuk branch OnHand vs Quarantine; `Expiry` untuk FEFO; `RejectedLines` =
// metadata return-to-vendor / excess (overview §A4).
public sealed record GRConfirmedV1(
    Guid GrId,
    string WarehouseId,
    IReadOnlyList<ReceivedLineV1> ReceivedLines,
    IReadOnlyList<RejectedLineV1> RejectedLines)
{
    public const string LogicalName = "inbound.gr_confirmed.v1";
}

// What: line yang diterima masuk inventory (published language)
// Status ∈ {"Good","QcHold"} → Inventory: Good→Stock(OnHand)+PutawayTask, QcHold→Stock(Quarantine).
public sealed record ReceivedLineV1(
    string Sku, int Quantity, string Status, string? Batch, DateOnly? Expiry);

// What: line yang ditolak (tak masuk inventory) — Reason ∈ {"ReturnToSupplier","RejectExcess"}
public sealed record RejectedLineV1(string Sku, int Quantity, string Reason);
