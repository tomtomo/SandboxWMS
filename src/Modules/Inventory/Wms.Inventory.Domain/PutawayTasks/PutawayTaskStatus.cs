namespace Wms.Inventory.Domain;

// What: lifecycle state PutawayTask (Phase 03b, overview §B)
// Why: instruksi pindah barang receiving→rak: Assigned saat dibuat (GRConfirmed), Completed saat
// operator selesai memindah & scan destination (CompletePutaway → Stock OnHand→Available).
// How: disimpan sebagai STRING (HasConversion<string>) — urutan numerik tak mengikat persistence.
public enum PutawayTaskStatus
{
    Assigned,
    Completed
}
