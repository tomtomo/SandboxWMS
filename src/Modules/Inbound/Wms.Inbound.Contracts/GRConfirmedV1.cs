namespace Wms.Inbound.Contracts;

// What: Integration Event (Published Language; ADR-0005 / ADR-0009)
// Why: kontrak publik ber-versi yang menyeberang broker — DECOUPLED dari tipe domain
// GoodsReceiptConfirmed yang in-process. POCO record, ZERO transport/serialization
// dependency (ADR-0009) sehingga konsumen tak terpaksa menarik stack apa pun; aman
// jadi versioned package saat polyrepo (ADR-0007).
// How: record immutable; LogicalName = identitas broker-facing `{module}.{event}.v{N}`
// (ADR-0023) yang dipakai dispatcher/consumer untuk routing — decoupled dari nama CLR
// (rename kelas bukan breaking change diam-diam).
//
// What: logical-name binding lewat `const string LogicalName` (POLA contract — Phase 02b)
// Why: ADR-0023 membolehkan attribute ATAU const. Dipilih `const` karena `*.Contracts`
// wajib dependency-free (blueprint §4: "──▶ (nothing)") biar tetap versioned-package
// candidate (ADR-0009); attribute butuh shared kernel (langgar zero-dep) atau duplikasi
// per-assembly. `const` reflection-discoverable + zero-dep — inilah marker yang dibaca
// FF #11 contract-coverage (asyncapi.yaml <-> tipe published). Record payload bersarang
// (ReceivedLineV1) sengaja TANPA const → bukan channel, hanya bagian payload.
public sealed record GRConfirmedV1(
    Guid GrId,
    string WarehouseId,
    IReadOnlyList<ReceivedLineV1> ReceivedLines)
{
    public const string LogicalName = "inbound.gr_confirmed.v1";
}

// What: line snapshot di payload integration event (published language)
public sealed record ReceivedLineV1(string Sku, int Quantity);
