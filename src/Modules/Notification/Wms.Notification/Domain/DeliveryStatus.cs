namespace Wms.Notification.Domain;

// What: lifecycle state NotificationDelivery — state machine (overview §G)
// Why: Pending (di-queue handler) → Sent (worker berhasil dispatch ke channel) | Failed (gagal,
// retry s/d max → DLQ). Read = user buka notif (hanya InApp). State jadi guard transisi legal +
// dasar idempotency (worker skip yang sudah Sent).
public enum DeliveryStatus
{
    Pending,
    Sent,
    Failed,
    Read
}
