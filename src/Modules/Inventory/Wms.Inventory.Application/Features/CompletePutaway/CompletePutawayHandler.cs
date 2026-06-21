using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Features.CompletePutaway;

// What: CQRS — Command Handler (MediatR) — selesaikan putaway (overview §B2)
// Why: satu use-case mengoordinasi DUA aggregate — PutawayTask Assigned→Completed DAN Stock
// OnHand→Available (lokasi pindah ke rak actualDestination) — dalam SATU transaksi (TransactionBehavior
// commit saat Result.Success, rollback saat Failure). Tiap aggregate menegakkan invariant-nya sendiri
// (legalitas transisi); handler hanya merangkai + memetakan kegagalan ke Result. No-throw (ADR-0019).
// How: load task → Complete(actual) → load Stock (task.StockId) → Putaway(actual) → SaveChanges (enlist
// ke transaksi pipeline). actualDestination yang sama mengikat task & lokasi rak final Stock.
public sealed class CompletePutawayHandler(
    IPutawayTaskRepository putawayTaskRepository,
    IStockRepository stockRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CompletePutawayCommand, Result>
{
    public async Task<Result> Handle(CompletePutawayCommand command, CancellationToken cancellationToken)
    {
        var task = await putawayTaskRepository.GetAsync(
            new PutawayTaskId(command.PutawayTaskId), cancellationToken);
        if (task is null)
            return Result.Failure(PutawayTaskErrors.NotFound);

        var complete = task.Complete(command.ActualDestinationId);
        if (complete.IsFailure)
            return complete;

        var stock = await stockRepository.GetAsync(task.StockId, cancellationToken);
        if (stock is null)
            return Result.Failure(StockErrors.NotFound);

        var putaway = stock.Putaway(command.ActualDestinationId);
        if (putaway.IsFailure)
            return putaway;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
