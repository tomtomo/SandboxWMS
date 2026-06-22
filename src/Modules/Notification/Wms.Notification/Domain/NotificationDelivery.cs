using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Notification.Domain;

// What: Aggregate Root (DDD) — NotificationDelivery, state machine (overview §G)
// Why: satu attempt pengiriman ke satu user via satu channel. Pintu konsistensi siklus
// Pending→Sent/Failed→Read: tiap transisi method ber-guard + Result (ADR-0019, no-throw FF#7).
// IDEMPOTENCY (ADR-0017): MarkSent dari state Sent = no-op sukses → worker re-deliver tak double-send.
// PLAIN AggregateRoot (bukan Auditable): di-author SYSTEM (handler enqueue + worker dispatch), pure
// consumer — audit-universal di-skip secara sadar (konsisten Reporting). Tak emit domain event
// (Notification read-only ke core, tak pernah publish balik — ADR-0017).
// How: factory Enqueue membuat state Pending; MarkSent/MarkFailed dipanggil worker; MarkRead via
// endpoint REST (hanya InApp). RetryCount menjejaki attempt gagal → worker DLQ saat exhausted.
public sealed class NotificationDelivery : AggregateRoot<NotificationDeliveryId>
{
    // null untuk delivery DIRECT (mis. operator dari payload event, tanpa subscription) — overview §G
    public NotificationSubscriptionId? SubscriptionId { get; private set; }

    public string UserId { get; private set; } = null!;

    public NotificationChannel Channel { get; private set; }

    // logical event sumber (mis. "inbound.gr_confirmed.v1") — konteks + grouping inbox in-app
    public string EventType { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string Body { get; private set; } = null!;

    // warehouse sumber event — worker meresolusi nama via MasterData read-API saat dispatch (ADR-0011)
    public string? WarehouseId { get; private set; }

    // korelasi ke event sumber (eventId) untuk forensik/trace (overview §G eventRef)
    public string EventRef { get; private set; } = null!;

    public DeliveryStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    private NotificationDelivery() { }

    private NotificationDelivery(
        NotificationDeliveryId id,
        NotificationSubscriptionId? subscriptionId,
        string userId,
        NotificationChannel channel,
        string eventType,
        string title,
        string body,
        string? warehouseId,
        string eventRef,
        DateTimeOffset queuedAt)
        : base(id)
    {
        SubscriptionId = subscriptionId;
        UserId = userId;
        Channel = channel;
        EventType = eventType;
        Title = title;
        Body = body;
        WarehouseId = warehouseId;
        EventRef = eventRef;
        Status = DeliveryStatus.Pending;
        QueuedAt = queuedAt;
    }

    // What: factory — enqueue delivery state Pending (di-commit handler, Inbox-committed ADR-0017)
    // Why: handler hanya MENULIS niat-kirim (DB-only, nol I/O eksternal di transaksi) — worker async
    // yang men-dispatch nanti, supaya flow event utama tak ke-block channel provider (overview §G).
    public static NotificationDelivery Enqueue(
        NotificationDeliveryId id,
        NotificationSubscriptionId? subscriptionId,
        string userId,
        NotificationChannel channel,
        string eventType,
        string title,
        string body,
        string? warehouseId,
        string eventRef,
        DateTimeOffset queuedAt)
        => new(id, subscriptionId, userId, channel, eventType, title, body, warehouseId, eventRef, queuedAt);

    // What: transisi Pending/Failed→Sent + idempotency (ADR-0017)
    // Why: worker berhasil dispatch ke channel. Dari Sent = no-op sukses (re-delivery at-least-once
    // tak boleh double-send) — INILAH guard idempotent "cek Sent sebelum commit kirim".
    public Result MarkSent(string providerMessageId, DateTimeOffset sentAt)
    {
        if (Status == DeliveryStatus.Sent || Status == DeliveryStatus.Read)
            return Result.Success();

        ProviderMessageId = providerMessageId;
        SentAt = sentAt;
        FailureReason = null;
        Status = DeliveryStatus.Sent;
        return Result.Success();
    }

    // What: transisi →Failed + increment retry (worker saat dispatch gagal)
    // Why: kegagalan channel di-isolasi (tak propagate ke core, ADR-0017); RetryCount menaik tiap
    // attempt → worker memutuskan retry lagi atau parkir ke DLQ saat HasExhaustedRetries.
    public Result MarkFailed(string reason)
    {
        if (Status == DeliveryStatus.Sent || Status == DeliveryStatus.Read)
            return Result.Failure(NotificationErrors.AlreadySent);

        RetryCount += 1;
        FailureReason = reason;
        Status = DeliveryStatus.Failed;
        return Result.Success();
    }

    // What: predikat batas-retry → ambang Dead Letter Channel (EIP; ADR-0005/0017)
    public bool HasExhaustedRetries(int maxAttempts) => RetryCount >= maxAttempts;

    // What: re-entrancy guard worker — sudah final (Sent/Read) → jangan kirim ulang
    public bool IsAlreadyDispatched => Status is DeliveryStatus.Sent or DeliveryStatus.Read;

    // What: transisi Sent→Read (overview §G) — HANYA channel InApp
    // Why: read-tracking cuma bermakna untuk in-app inbox (Email/Push tak di-track, ADR-0017 deferred).
    public Result MarkRead(DateTimeOffset readAt)
    {
        if (Channel != NotificationChannel.InApp || Status != DeliveryStatus.Sent)
            return Result.Failure(NotificationErrors.NotReadable);

        Status = DeliveryStatus.Read;
        ReadAt = readAt;
        return Result.Success();
    }
}
