using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Contracts;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Features.CompletePutaway;

// What: CQRS — Command Handler (MediatR) + emit PutawayCompleted (overview §B2; ADR-0005/0030)
// Why: satu use-case mengoordinasi DUA aggregate — PutawayTask Assigned→Completed DAN Stock
// OnHand→Available (lokasi pindah ke rak actualDestination) — dalam SATU transaksi (TransactionBehavior
// commit saat Result.Success, rollback saat Failure). Tiap aggregate menegakkan invariant-nya sendiri
// (legalitas transisi); handler hanya merangkai + memetakan kegagalan ke Result. No-throw (ADR-0019).
// Putaway selesai = fakta yang dibutuhkan Reporting OperatorActivity (putaway-count per operator) — Inventory
// pemilik PutawayTask/Stock mengemit PutawayCompletedV1 (ADR-0030); operatorId = aktor (ICurrentUser).
// How: load task → Complete(actual) → load Stock (task.StockId) → Putaway(actual) → Enqueue PutawayCompleted
// → SaveChanges (enlist transaksi pipeline; outbox + state atomic). actualDestination mengikat task & rak final.
public sealed class CompletePutawayHandler(
    IPutawayTaskRepository putawayTaskRepository,
    IStockRepository stockRepository,
    IIntegrationEventOutbox outbox,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CompletePutawayCommand, Result>
{
    public async Task<Result> Handle(CompletePutawayCommand command, CancellationToken cancellationToken)
    {
        var task = await putawayTaskRepository.GetByIdAsync(
            new PutawayTaskId(command.PutawayTaskId), cancellationToken);
        if (task is null)
            return Result.Failure(PutawayTaskErrors.NotFound);

        var complete = task.Complete(command.ActualDestinationId);
        if (complete.IsFailure)
            return complete;

        var stock = await stockRepository.GetByIdAsync(task.StockId, cancellationToken);
        if (stock is null)
            return Result.Failure(StockErrors.NotFound);

        var putaway = stock.Putaway(command.ActualDestinationId);
        if (putaway.IsFailure)
            return putaway;

        // emit hanya pada fakta sukses (ADR-0026): operatorId = aktor penyelesai (ICurrentUser, SYSTEM
        // s/d authZ 07a — ADR-0012/0027). Enqueue + state commit SATU transaksi (anti dual-write).
        outbox.Enqueue(ToEnvelope(task, stock, currentUser.UserId));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // What: Message Translator (EIP) — putaway selesai → integration event envelope (ADR-0005/0030)
    // How: EventId baru = identitas outbox/idempotency (Reporting dedup via Inbox → OperatorActivity
    // putaway-count per operator). Warehouse/sku dari Stock (konteks; operator dari aktor handler).
    private static MessageEnvelope ToEnvelope(PutawayTask task, Stock stock, string operatorId)
    {
        var payload = new PutawayCompletedV1(
            task.Id.Value, stock.Id.Value, stock.Sku, stock.WarehouseId, operatorId);

        return MessageEnvelope.For(PutawayCompletedV1.LogicalName, payload);
    }
}
