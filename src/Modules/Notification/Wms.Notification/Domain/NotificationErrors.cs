using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Notification.Domain;

// What: katalog Error domain Notification (Result pattern, ADR-0019)
// Why: kegagalan bisnis sebagai NILAI ber-Code stabil (bukan exception, FF#7) — caller dipaksa
// handle eksplisit; Code dipakai untuk mapping transport (ProblemDetails) di endpoint.
public static class NotificationErrors
{
    // --- NotificationSubscription factory invariants ---
    public static readonly Error MissingSubscriber =
        Error.Validation("notification_subscription.missing_subscriber", "subscriberId wajib diisi.");

    public static readonly Error MissingEventType =
        Error.Validation("notification_subscription.missing_event_type", "eventType wajib diisi.");

    public static readonly Error NoChannels =
        Error.Validation("notification_subscription.no_channels", "subscription minimal punya satu channel.");

    // --- NotificationDelivery state-transition guards ---
    public static readonly Error AlreadySent =
        Error.Conflict("notification_delivery.already_sent", "delivery sudah Sent — tak bisa ditandai gagal.");

    public static readonly Error NotReadable =
        Error.Conflict("notification_delivery.not_readable", "mark-as-read hanya untuk channel InApp yang sudah Sent.");

    public static readonly Error NotFound =
        Error.NotFound("notification_delivery.not_found", "NotificationDelivery tidak ditemukan.");
}
