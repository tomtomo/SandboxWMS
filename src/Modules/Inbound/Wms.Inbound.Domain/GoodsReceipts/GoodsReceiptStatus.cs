namespace Wms.Inbound.Domain;

// What: lifecycle state GoodsReceipt — minimal walking-skeleton (Phase 01c)
// Why: skeleton hanya butuh transisi Create→Confirm; Pending/Hold + two-axis
// discrepancy (ADR-0013) menyusul di Phase 03a — jangan dibangun dini.
public enum GoodsReceiptStatus
{
    InProgress,
    Confirmed
}
