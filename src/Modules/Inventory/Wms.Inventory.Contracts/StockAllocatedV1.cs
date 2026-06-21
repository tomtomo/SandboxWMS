namespace Wms.Inventory.Contracts;

// What: Integration Event (Published Language; ADR-0005 / ADR-0009) — Inventory → Outbound
// Why: setelah semua line WaveReleased ter-alokasi FEFO (overview §C3), Inventory mengumumkan
// hasil alokasi agar Outbound membuat PickingTask per entry. Inilah satu-satunya event yang
// DIPANCARKAN Inventory di core flow. Strategi alokasi (FEFO) TETAP internal — tak bocor ke kontrak;
// yang menyeberang hanya HASIL-nya (stock terpilih per line).
// How: record immutable, POCO ZERO transport dep (ADR-0009); LogicalName broker-facing (ADR-0023)
// terdaftar di docs/architecture/asyncapi.yaml (FF#11). Ditulis ke Outbox oleh consumer WaveReleased
// dalam SATU transaksi dengan transisi Stock + Inbox-mark (anti dual-write).
public sealed record StockAllocatedV1(
    Guid WaveId,
    IReadOnlyList<StockAllocationV1> Allocations)
{
    public const string LogicalName = "inventory.stock_allocated.v1";
}

// What: satu hasil alokasi stock per line wave (published language)
// Why: membawa identitas Stock terpilih + lokasi/batch/qty sehingga Outbound bisa membuat PickingTask
// yang mengarah ke rak fisik yang benar. Batch nullable (produk tanpa batch-tracking).
public sealed record StockAllocationV1(
    string Sku, string LocationId, string? Batch, int Qty, Guid StockId);
