namespace Wms.Outbound.Contracts;

// What: Integration Event (Published Language; ADR-0005 / ADR-0009) — Outbound → Inventory
// Why: saat SPV membuat Wave (overview §C2), Outbound memerintahkan Inventory mengalokasi stock.
// Kontrak publik ber-versi yang menyeberang broker — DECOUPLED dari domain Wave (in-process).
// POCO record, ZERO transport/serialization dependency (ADR-0009) → konsumen tak menarik stack apa pun;
// aman jadi versioned package saat polyrepo (ADR-0007).
// How: record immutable; LogicalName = identitas broker-facing `{module}.{event}.v{N}` (ADR-0023),
// terdaftar di docs/architecture/asyncapi.yaml (FF#11). Lahir di Phase 03b (consumer-first):
// Inventory meng-consume di 03b; emitter Wave aggregate menyusul 03c — tipe yang sama dipakai ulang.
public sealed record WaveReleasedV1(
    Guid WaveId,
    IReadOnlyList<WaveLineV1> Lines)
{
    public const string LogicalName = "outbound.wave_released.v1";
}

// What: satu baris permintaan alokasi per (order, sku) dalam sebuah wave (published language)
// Why: Inventory hanya butuh sku+qty untuk FEFO; orderId dibawa untuk korelasi downstream
// (PickingTask per order di Outbound) — Inventory mengabaikannya (ACL: ambil yang relevan saja).
public sealed record WaveLineV1(Guid OrderId, string Sku, int Qty);
