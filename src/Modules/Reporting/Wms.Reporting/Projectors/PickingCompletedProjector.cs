using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Outbound.Contracts;
using Wms.Reporting.Stores;

namespace Wms.Reporting.Projectors;

// What: Idempotent projection consumer (EIP; ADR-0005/0017/0030) — PickingCompleted → OperatorActivity
// Why: sisi Reporting dari produktivitas — increment pick-count per operator/hari. operatorId di-bawa event
// (enrichment ADR-0030; SYSTEM s/d authZ 07a). Inbox-committed atomicity (ADR-0017): satu transaksi.
// How: cek Inbox → store.ApplyPick (NO SaveChanges) → MarkProcessed → SaveChanges. occurredAt → bucket hari.
public sealed class PickingCompletedProjector(
    IOperatorActivityStore operatorActivityStore,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "reporting.picking-completed";

    public async Task<Result> HandleAsync(
        Guid eventId, DateTimeOffset occurredAt, PickingCompletedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var day = DateOnly.FromDateTime(occurredAt.UtcDateTime);
        await operatorActivityStore.ApplyPickAsync(message.OperatorId, day, cancellationToken);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
