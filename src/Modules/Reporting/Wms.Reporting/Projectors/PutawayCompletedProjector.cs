using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Contracts;
using Wms.Reporting.Stores;

namespace Wms.Reporting.Projectors;

// What: Idempotent projection consumer (EIP; ADR-0005/0017/0030) — PutawayCompleted → OperatorActivity
// Why: sisi Reporting dari produktivitas — increment putaway-count per operator/hari. Inbox-committed
// atomicity (ADR-0017): projection-write + Inbox-mark satu transaksi. operatorId = SYSTEM s/d authZ (07a).
// How: cek Inbox → store.ApplyPutaway (NO SaveChanges) → MarkProcessed → SaveChanges. occurredAt → bucket hari.
public sealed class PutawayCompletedProjector(
    IOperatorActivityStore operatorActivityStore,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "reporting.putaway-completed";

    public async Task<Result> HandleAsync(
        Guid eventId, DateTimeOffset occurredAt, PutawayCompletedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var day = DateOnly.FromDateTime(occurredAt.UtcDateTime);
        await operatorActivityStore.ApplyPutawayAsync(message.OperatorId, day, cancellationToken);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
