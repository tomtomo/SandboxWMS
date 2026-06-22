using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Notification.Domain;
using Wms.Notification.Persistence;

namespace Wms.Notification.Endpoints;

// What: REST endpoints Notification (CQRS read-side + command tipis; ADR-0006/0017/0019)
// Why: (a) seed subscription (admin/WebUI); (b) in-app inbox query (WebUI 04e baca delivery InApp);
// (c) mark-as-read (overview §G, hanya InApp). Module COLLAPSED (bukan layer Api terpisah) → baca/tulis
// NotificationDbContext di sini benar (FF#8 tak berlaku). Inbox paginated (PagedResult) — cegah unbounded
// result set. Result→HTTP via ToProblemDetails (ADR-0019). AuthZ deferred (ADR-0012) → TODO-AUTH; 07a.
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Notification.ManageSubscription
        app.MapPost("/notifications/subscriptions", async (
            CreateSubscriptionRequest request, NotificationDbContext db, CancellationToken cancellationToken) =>
        {
            var result = NotificationSubscription.Create(
                NotificationSubscriptionId.New(), request.SubscriberType, request.SubscriberId,
                request.EventType, request.Channels, request.WarehouseScope);
            if (result.IsFailure)
                return result.ToProblemDetails();

            db.Subscriptions.Add(result.Value);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created(
                $"/notifications/subscriptions/{result.Value.Id.Value}", new { id = result.Value.Id.Value });
        });

        // TODO-AUTH: Notification.ViewInbox — in-app inbox per user (Sent/Read), terbaru dulu, paginated
        app.MapGet("/notifications/inbox", async (
            string userId, NotificationDbContext db,
            int? page, int? pageSize, CancellationToken cancellationToken) =>
        {
            var safePage = Math.Max(1, page ?? 1);
            var safeSize = Math.Clamp(pageSize ?? 20, 1, 100);

            var query = db.Deliveries.AsNoTracking()
                .Where(delivery => delivery.UserId == userId
                    && delivery.Channel == NotificationChannel.InApp
                    && (delivery.Status == DeliveryStatus.Sent || delivery.Status == DeliveryStatus.Read));

            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderByDescending(delivery => delivery.QueuedAt)
                .Skip((safePage - 1) * safeSize).Take(safeSize)
                .Select(delivery => new InAppNotificationRow(
                    delivery.Id.Value, delivery.EventType, delivery.Title, delivery.Body,
                    delivery.Status.ToString(), delivery.QueuedAt, delivery.ReadAt))
                .ToListAsync(cancellationToken);
            return Results.Ok(new PagedResult<InAppNotificationRow>(rows, safePage, safeSize, total));
        });

        // TODO-AUTH: Notification.MarkRead — overview §G (hanya InApp)
        app.MapPost("/notifications/deliveries/{id:guid}/read", async (
            Guid id, NotificationDbContext db, CancellationToken cancellationToken) =>
        {
            var delivery = await db.Deliveries.FindAsync([new NotificationDeliveryId(id)], cancellationToken);
            if (delivery is null)
                return NotificationErrors.NotFound.ToProblemDetails();

            var result = delivery.MarkRead(DateTimeOffset.UtcNow);
            if (result.IsFailure)
                return result.ToProblemDetails();

            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}

// What: request DTO create subscription — enum di-bind dari string via JsonStringEnumConverter (host)
public sealed record CreateSubscriptionRequest(
    SubscriberType SubscriberType,
    string SubscriberId,
    string EventType,
    IReadOnlyList<NotificationChannel> Channels,
    string? WarehouseScope);

// What: read DTO in-app inbox (CQRS read-side) — bentuk respons, decoupled dari aggregate
public sealed record InAppNotificationRow(
    Guid DeliveryId, string EventType, string Title, string Body, string Status,
    DateTimeOffset QueuedAt, DateTimeOffset? ReadAt);
