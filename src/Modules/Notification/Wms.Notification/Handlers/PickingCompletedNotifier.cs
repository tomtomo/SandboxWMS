using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Notification.Domain;
using Wms.Notification.Subscriptions;
using Wms.Outbound.Contracts;

namespace Wms.Notification.Handlers;

// What: Idempotent event consumer (EIP Idempotent Receiver; ADR-0005/0017) — PickingCompleted → notifikasi
// Why: sisi Notification dari picking. Trigger §G "PickingTask → operator" via recipient DIRECT (userId =
// OperatorId di payload, BUKAN subscription role-based) → mendemonstrasikan jalur recipient kedua. Momen
// §G adalah "Assigned"; di-proxy "Completed" (event yang ADA — momen assigned butuh event baru, di-defer).
// OperatorId nullable/SYSTEM s/d authZ 07a → kirim hanya bila operator riil (else inert, di-skip sadar).
// How: cek Inbox → enqueueDirect (Add delivery Pending) → MarkProcessed → SaveChanges (satu tx Inbox-committed).
public sealed class PickingCompletedNotifier(
    NotificationEnqueuer enqueuer,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    public const string HandlerType = "notification.picking-completed";

    // aktor mesin/authZ-deferred (ADR-0012/0027) — delivery ke SYSTEM inert, di-skip
    private const string SystemActor = "SYSTEM";

    public async Task<Result> HandleAsync(
        Guid eventId, DateTimeOffset occurredAt, PickingCompletedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        if (!string.IsNullOrWhiteSpace(message.OperatorId)
            && !string.Equals(message.OperatorId, SystemActor, StringComparison.Ordinal))
            enqueuer.EnqueueDirect(
                message.OperatorId, NotificationChannel.InApp,
                PickingCompletedV1.LogicalName,
                title: "Picking selesai",
                body: $"PickingTask {message.PickingTaskId} untuk SKU {message.Sku} telah selesai.",
                warehouseId: null, eventId.ToString(), occurredAt);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
