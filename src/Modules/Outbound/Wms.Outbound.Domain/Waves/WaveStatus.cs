namespace Wms.Outbound.Domain;

// What: lifecycle state Wave (Phase 03c, overview §C)
// Why: grouping order untuk picking/dispatch bersama. Active saat dibuat (order masuk, emit WaveReleased);
// Ready saat SEMUA PickingTask Completed (siap dispatch); Dispatched saat truk keluar (emit ShipmentDispatched).
// How: disimpan sebagai STRING (HasConversion<string>) — urutan numerik enum tak mengikat persistence.
public enum WaveStatus
{
    Active,
    Ready,
    Dispatched
}
