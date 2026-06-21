namespace Wms.Inventory.Domain;

// What: lifecycle state Stock — penuh (Phase 03b, overview §B)
// Why: balance fisik melewati siklus yang ditransisikan trigger event-driven berbeda. Quarantine &
// OnHand = state masuk (dari GRConfirmed: QcHold vs Good); Available = sudah di rak (putaway);
// Allocated = direservasi ke wave; Picked = di staging menunggu dispatch (lalu removed).
// How: disimpan sebagai STRING (StockConfiguration HasConversion<string>) → urutan numerik enum tak
// mengikat persistence; aman ditambah/diurut ulang non-breaking.
public enum StockStatus
{
    Quarantine,
    OnHand,
    Available,
    Allocated,
    Picked
}
