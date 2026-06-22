using Microsoft.EntityFrameworkCore;
using Wms.Notification.Domain;
using Wms.Notification.Persistence;

namespace Wms.Notification.Subscriptions;

// What: Domain Service — resolusi subscription → enqueue NotificationDelivery (overview §G)
// Why: logika "siapa penerima, lewat channel apa" dipakai bersama oleh banyak notifier (GR, picking, …) →
// di-ekstrak satu tempat. Beroperasi atas NotificationDbContext AMBIENT yang SAMA dgn IInboxGuard/IUnitOfWork
// notifier → semua Add ter-commit dalam SATU transaksi Inbox-committed (ADR-0017). NOL I/O eksternal di sini
// (resolusi recipient email/warehouse di-defer ke worker) → transaksi handler tetap murni DB.
// How: query subscription aktif by (eventType, warehouseScope) → resolve recipients → enqueue Pending per
// (recipient × channel). EnqueueDirect untuk recipient dari payload event (mis. operator) tanpa subscription.
public sealed class NotificationEnqueuer(NotificationDbContext db)
{
    public async Task EnqueueForSubscribersAsync(
        string eventType,
        string? warehouseId,
        string title,
        string body,
        string eventRef,
        DateTimeOffset queuedAt,
        CancellationToken cancellationToken = default)
    {
        // warehouseScope null = subscription lintas-warehouse; selain itu harus cocok warehouse event
        var subscriptions = await db.Subscriptions
            .Where(subscription => subscription.IsActive
                && subscription.EventType == eventType
                && (subscription.WarehouseScope == null || subscription.WarehouseScope == warehouseId))
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
            foreach (var userId in ResolveRecipients(subscription))
                foreach (var channel in subscription.Channels)
                    db.Deliveries.Add(NotificationDelivery.Enqueue(
                        NotificationDeliveryId.New(), subscription.Id, userId, channel,
                        eventType, title, body, warehouseId, eventRef, queuedAt));
    }

    // What: enqueue DIRECT — recipient userId dari payload event (mis. operator), tanpa subscription
    public void EnqueueDirect(
        string userId,
        NotificationChannel channel,
        string eventType,
        string title,
        string body,
        string? warehouseId,
        string eventRef,
        DateTimeOffset queuedAt)
        => db.Deliveries.Add(NotificationDelivery.Enqueue(
            NotificationDeliveryId.New(), subscriptionId: null, userId, channel,
            eventType, title, body, warehouseId, eventRef, queuedAt));

    // What: resolusi recipient dari subscription (overview §G subscriberType)
    // Why: User → recipient langsung. Role → fan-out ke semua user ber-role itu, tapi Auth read-API
    // BELUM punya ListUsersByRole (gap 04d, di-defer ke enrich Auth nanti) → Role mengembalikan kosong
    // SADAR (subscription Role tercatat tapi belum mewujud delivery). Model tetap menampung Role agar
    // spec §G utuh; mechanism-first (pilihan Tom) merealisasikan path User-direct lebih dulu.
    private static IEnumerable<string> ResolveRecipients(NotificationSubscription subscription) =>
        subscription.SubscriberType switch
        {
            SubscriberType.User => [subscription.SubscriberId],
            _ => []
        };
}
