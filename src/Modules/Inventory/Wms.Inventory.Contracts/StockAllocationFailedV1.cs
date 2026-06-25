namespace Wms.Inventory.Contracts;

// What: Integration Event (Published Language; ADR-0005 / ADR-0009 / ADR-0034) — Inventory → Outbound + Notification
// Why: saat alokasi FEFO WaveReleased TAK terpenuhi penuh untuk sebuah line (stock Available kurang/nol),
// Inventory mengumumkan kekurangan secara EKSPLISIT — ganti silent-drop lama (overview §B#2). Outbound
// menandai OrderLine Short/Backordered; Notification meng-alert. Pola sinyal-gagal ala eShop OrderStockRejected,
// tapi eventual (bukan sync ATP-gate). Hanya line yang KURANG yang dibawa (line penuh lewat StockAllocated).
// How: record immutable, POCO ZERO transport dep (ADR-0009); LogicalName broker-facing (ADR-0023) terdaftar di
// docs/architecture/asyncapi.yaml (FF#11). Ditulis ke Outbox oleh WaveReleasedConsumer dalam SATU transaksi
// dengan alokasi + Inbox-mark (anti dual-write). Konsistensi qty: allocatedQty (StockAllocated) + shortQty == requestedQty.
public sealed record StockAllocationFailedV1(
    Guid WaveId,
    IReadOnlyList<StockAllocationFailedLineV1> Lines)
{
    public const string LogicalName = "inventory.stock_allocation_failed.v1";
}

// What: satu line yang tak teralokasi penuh (published language)
// Why: orderId+sku → Outbound petakan ke OrderLine; requested/allocated/short → besaran kekurangan untuk
// status line + pesan notifikasi. allocatedQty 0 = sama sekali tak ada stock; >0 = teralokasi sebagian.
public sealed record StockAllocationFailedLineV1(
    Guid OrderId, string Sku, int RequestedQty, int AllocatedQty, int ShortQty);
