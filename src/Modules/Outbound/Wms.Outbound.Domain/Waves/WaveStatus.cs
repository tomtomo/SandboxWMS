namespace Wms.Outbound.Domain;

// What: lifecycle state Wave (Phase 03c, overview §C; ADR-0035)
// Why: grouping order untuk picking/dispatch bersama. Active saat dibuat (order masuk, emit WaveReleased);
// Ready saat SEMUA PickingTask Completed (siap dispatch); Dispatched saat truk keluar (emit ShipmentDispatched);
// Cancelled saat wave nol-terpenuhi (stock nol) → auto-bubar, order balik backlog (ADR-0035). Cancelled & Dispatched = terminal.
// How: disimpan sebagai STRING (HasConversion<string>) — urutan numerik enum tak mengikat persistence; nilai baru tanpa migrasi schema.
public enum WaveStatus
{
    Active,
    Ready,
    Dispatched,
    Cancelled
}
