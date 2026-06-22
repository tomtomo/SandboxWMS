namespace Wms.Notification.Domain;

// What: jenis subscriber NotificationSubscription (overview §G)
// Why: User = recipient langsung (1 userId); Role = fan-out ke semua user ber-role itu.
// Role fan-out butuh Auth read-API ListUsersByRole yang BELUM ada (gap 04d, di-defer) — model
// tetap menampung Role agar spec §G utuh; resolver hanya merealisasikan User di scope ini.
public enum SubscriberType
{
    User,
    Role
}
