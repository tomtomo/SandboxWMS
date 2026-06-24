using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Features.AdjustStock;

// What: CQRS — Command Handler (MediatR) — koreksi manual kuantitas Stock
// Why: satu use-case memuat Stock via repository (tracked) → Stock.Adjust (invariant non-negatif di
// domain) → SaveChanges (TransactionBehavior commit saat Success, rollback saat Failure). No-throw
// (ADR-0019): NotFound → StockErrors.NotFound. Tak meng-emit event (tak ada konsumen downstream).
// How: load(new StockId(id)) → Adjust(newQty) → unitOfWork.SaveChangesAsync.
public sealed class AdjustStockHandler(
    IStockRepository stockRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AdjustStockCommand, Result>
{
    public async Task<Result> Handle(AdjustStockCommand command, CancellationToken cancellationToken)
    {
        var stock = await stockRepository.GetByIdAsync(new StockId(command.StockId), cancellationToken);
        if (stock is null)
            return Result.Failure(StockErrors.NotFound);

        var adjust = stock.Adjust(command.NewQty);
        if (adjust.IsFailure)
            return adjust;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
