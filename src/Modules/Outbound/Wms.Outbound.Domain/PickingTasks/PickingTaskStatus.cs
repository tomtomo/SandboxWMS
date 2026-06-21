namespace Wms.Outbound.Domain;

// What: lifecycle state PickingTask (Phase 03c, overview §C)
// Why: instruksi ambil stock dari rak ke staging: Assigned saat dibuat (StockAllocated dikonsumsi),
// Completed saat operator selesai pick & scan staging (CompletePicking → emit PickingCompleted).
// How: disimpan sebagai STRING (HasConversion<string>) — urutan numerik tak mengikat persistence.
public enum PickingTaskStatus
{
    Assigned,
    Completed
}
