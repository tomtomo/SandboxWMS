using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;
using Wms.Outbound.Contracts;

namespace Wms.Inventory.Application.Features.ConsumePickingCompleted;

// What: Idempotent consumer PickingCompleted → Stock Allocated→Picked (EIP; ADR-0005 / ADR-0028)
// Why: realisasi sinyal overview §B/§C5 — PickingTask Assigned→Completed di Outbound memicu Stock
// pindah ke staging (Picked). Karena Stock milik Inventory & PickingTask milik Outbound (DB-per-service,
// ADR-0010), transisi mengalir via event ini (ADR-0028), bukan shared store. ACL: PickingCompletedV1
// membawa stockId → langsung target Stock spesifik.
// How: cek Inbox (eventId, HandlerType). Get Stock by id; stock.Pick(pickingTaskId, staging) — transisi +
// guard (hanya Allocated) di domain. MarkProcessed + SaveChanges satu transaksi. Stock hilang = integrity
// error → Result.Failure (DLQ forensik), bukan diam.
public sealed class PickingCompletedConsumer(
    IStockRepository stockRepository,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "inventory.picking-completed";

    public async Task<Result> HandleAsync(
        Guid eventId, PickingCompletedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var stock = await stockRepository.GetByIdAsync(new StockId(message.StockId), cancellationToken);
        if (stock is null)
            return Result.Failure(StockErrors.NotFound);

        var pick = stock.Pick(message.PickingTaskId, message.StagingLocationId);
        if (pick.IsFailure)
            return Result.Failure(pick.Error);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
