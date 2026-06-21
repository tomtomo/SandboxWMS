namespace Wms.Inbound.Domain;

// What: lifecycle state GoodsReceipt — state machine penuh (Phase 03a, overview §A)
// Why: penerimaan barang melewati tahap eksplisit: scan (InProgress) → review discrepancy
// (Pending) → keputusan SPV (Confirmed atau Hold). State jadi guard transisi legal di aggregate.
public enum GoodsReceiptStatus
{
    InProgress,
    Pending,
    Confirmed,
    Hold
}
