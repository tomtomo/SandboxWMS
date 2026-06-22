using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Notification.Domain;

// What: Aggregate Root (DDD) — NotificationSubscription (overview §G)
// Why: aturan "siapa dapat notifikasi apa, lewat channel mana" — sumber kebenaran routing notifikasi.
// Pintu konsistensi: invariant (subscriber, eventType, channels non-empty) ditegakkan DI SINI via
// factory Create → Result (ADR-0019), bukan di handler. PLAIN AggregateRoot (bukan Auditable):
// Notification = pure consumer (ADR-0017), subscription di-seed/di-kelola SYSTEM — audit-universal
// di-skip secara sadar (konsisten profil Reporting), sama spt projection read-side.
// How: Channels dipetakan EF lewat value-converter ke kolom tunggal (lihat configuration). Aggregate
// efektif immutable kecuali Deactivate (soft-delete via IsActive, overview §G).
public sealed class NotificationSubscription : AggregateRoot<NotificationSubscriptionId>
{
    public SubscriberType SubscriberType { get; private set; }

    // userId (SubscriberType.User) atau roleId/roleCode (SubscriberType.Role)
    public string SubscriberId { get; private set; } = null!;

    // logical event yang di-subscribe — di-match dengan trigger handler (mis. "inbound.gr_confirmed.v1")
    public string EventType { get; private set; } = null!;

    public IReadOnlyList<NotificationChannel> Channels { get; private set; } = [];

    // optional warehouse filter (overview §G) — null = berlaku lintas-warehouse
    public string? WarehouseScope { get; private set; }

    public bool IsActive { get; private set; }

    private NotificationSubscription() { }

    private NotificationSubscription(
        NotificationSubscriptionId id,
        SubscriberType subscriberType,
        string subscriberId,
        string eventType,
        IReadOnlyList<NotificationChannel> channels,
        string? warehouseScope)
        : base(id)
    {
        SubscriberType = subscriberType;
        SubscriberId = subscriberId;
        EventType = eventType;
        Channels = channels;
        WarehouseScope = warehouseScope;
        IsActive = true;
    }

    // What: factory + invariant guard (Result pattern, ADR-0019)
    // Why: subscription tanpa subscriber / eventType / channel tak bermakna — tolak di pintu masuk.
    public static Result<NotificationSubscription> Create(
        NotificationSubscriptionId id,
        SubscriberType subscriberType,
        string subscriberId,
        string eventType,
        IReadOnlyList<NotificationChannel> channels,
        string? warehouseScope = null)
    {
        if (string.IsNullOrWhiteSpace(subscriberId))
            return Result.Failure<NotificationSubscription>(NotificationErrors.MissingSubscriber);
        if (string.IsNullOrWhiteSpace(eventType))
            return Result.Failure<NotificationSubscription>(NotificationErrors.MissingEventType);
        if (channels.Count == 0)
            return Result.Failure<NotificationSubscription>(NotificationErrors.NoChannels);

        return Result.Success(new NotificationSubscription(
            id, subscriberType, subscriberId, eventType, channels.Distinct().ToList(), warehouseScope));
    }

    // What: soft-delete (overview §G isActive) — subscription berhenti memicu delivery tanpa di-hard-delete
    public void Deactivate() => IsActive = false;
}
