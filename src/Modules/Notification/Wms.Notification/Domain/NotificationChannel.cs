namespace Wms.Notification.Domain;

// What: channel pengiriman notifikasi (overview §G)
// Why: satu subscription bisa menuntut >1 channel → satu NotificationDelivery di-enqueue PER channel.
// Read-tracking (mark-as-read) hanya berlaku untuk InApp; Email/Push tak di-track (ADR-0017 deferred).
public enum NotificationChannel
{
    InApp,
    Email,
    Push
}
