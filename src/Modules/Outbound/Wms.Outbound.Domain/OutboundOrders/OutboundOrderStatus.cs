namespace Wms.Outbound.Domain;

// What: lifecycle state OutboundOrder (Phase 03c, overview §C)
// Why: order pelanggan: New saat masuk WMS (belum di-wave), InProgress saat masuk wave aktif (alokasi
// berjalan), Closed saat wave dispatch (selesai dari sisi WMS).
// How: disimpan sebagai STRING (HasConversion<string>) — urutan numerik enum tak mengikat persistence.
public enum OutboundOrderStatus
{
    New,
    InProgress,
    Closed
}
