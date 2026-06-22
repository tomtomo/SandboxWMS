namespace Wms.Inventory.Contracts;

// What: Integration Event (Published Language; ADR-0005 / ADR-0009 / ADR-0030) — Inventory → Reporting
// Why: overview §F: StockOnHandView DECREMENT saat barang keluar gudang + DispatchSummary (volume +
// throughput). Sumber datanya = Stock yang dihapus saat dispatch (overview §B `Picked→removed`). Outbound
// `shipment_dispatched` cuma bawa waveId — tak punya warehouse/sku/qty; INVENTORI pemilik Stock-nya (punya
// WarehouseId/qty). Maka Inventory-lah yang mengemit fakta "stock keluar" (ownership, ADR-0010/0030),
// melayani DUA projection dari satu event ownership-correct. Reporting pure consumer (ADR-0017).
// How: record immutable, POCO ZERO transport dep (ADR-0009). Di-emit consumer ShipmentDispatched dalam SATU
// transaksi dgn removal Stock + Inbox-mark (anti dual-write). lines[] = stock yang dihapus (warehouse/sku/
// batch/qty). LogicalName terdaftar di asyncapi.yaml (FF#11). Batch nullable (produk tanpa batch-tracking).
public sealed record StockRemovedV1(
    Guid WaveId,
    IReadOnlyList<StockRemovedLineV1> Lines)
{
    public const string LogicalName = "inventory.stock_removed.v1";
}

// What: satu baris stock yang keluar gudang (published language) — dimensi StockOnHandView (ADR-0030)
// Why: Reporting decrement per (warehouseId, sku, batch) + akumulasi volume DispatchSummary. Warehouse
// di-bawa karena hanya Inventory yang punya-nya saat removal (Outbound tak punya).
public sealed record StockRemovedLineV1(
    string WarehouseId, string Sku, string? Batch, int Qty);
