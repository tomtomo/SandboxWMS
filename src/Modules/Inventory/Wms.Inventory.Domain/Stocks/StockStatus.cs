namespace Wms.Inventory.Domain;

// What: lifecycle state Stock — minimal walking-skeleton (Phase 01c)
// Why: skeleton hanya menghidupkan jalur OnHand (hasil GRConfirmed lineStatus=Good).
// Quarantine/Available/Allocated/Picked + transisinya menyusul di Phase 03b — enum
// tumbuh non-breaking, jangan dibangun dini.
public enum StockStatus
{
    OnHand
}
