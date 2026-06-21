namespace Wms.Inbound.Contracts;

// What: Integration Event (Published Language; ADR-0005 / ADR-0009)
// Why: kontrak publik ber-versi yang menyeberang broker — DECOUPLED dari tipe domain
// GoodsReceiptConfirmed yang in-process. POCO record, ZERO transport/serialization
// dependency (ADR-0009) sehingga konsumen tak terpaksa menarik stack apa pun; aman
// jadi versioned package saat polyrepo (ADR-0007).
// How: record immutable; LogicalName = identitas broker-facing `{module}.{event}.v{N}`
// (ADR-0023) yang dipakai dispatcher/consumer untuk routing — decoupled dari nama CLR
// (rename kelas bukan breaking change diam-diam). Kelak dipindah ke attribute/registry
// saat AsyncAPI catalog + FF#11 (Phase 02b) menuntut discoverability.
public sealed record GRConfirmedV1(
    Guid GrId,
    string WarehouseId,
    IReadOnlyList<ReceivedLineV1> ReceivedLines)
{
    public const string LogicalName = "inbound.gr_confirmed.v1";
}

// What: line snapshot di payload integration event (published language)
public sealed record ReceivedLineV1(string Sku, int Quantity);
