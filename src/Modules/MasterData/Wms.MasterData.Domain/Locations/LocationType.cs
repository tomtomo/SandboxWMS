namespace Wms.MasterData.Domain;

// What: tipe lokasi fisik (overview §D Location) — menentukan peran lokasi di core flow
// Why: ReceivingArea (Stock OnHand sehabis GRConfirmed) · Rack (Available/Allocated) · QuarantineArea
// (Quarantine/QcHold) · StagingArea (Picked menunggu dispatch). Hierarki nested (Zone/Aisle/Bin)
// di-treat flat dgn code human-readable di scope ini (overview §D catatan).
// How: disimpan STRING (LocationConfiguration HasConversion<string>) → urutan numerik enum tak
// mengikat persistence; aman ditambah/diurut ulang non-breaking.
public enum LocationType
{
    ReceivingArea,
    Rack,
    QuarantineArea,
    StagingArea
}
