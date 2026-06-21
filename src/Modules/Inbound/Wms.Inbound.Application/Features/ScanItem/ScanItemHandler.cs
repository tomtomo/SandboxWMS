using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.ScanItem;

// What: CQRS — Command Handler (MediatR) — append scan ke aggregate (ADR-0004)
// Why: vertical slice; load GR → ScanItem (guard state + invariant di domain) → persist. Result
// no-throw (ADR-0019); transaksi oleh TransactionBehavior.
public sealed class ScanItemHandler(IGoodsReceiptRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<ScanItemCommand, Result>
{
    public async Task<Result> Handle(ScanItemCommand command, CancellationToken cancellationToken)
    {
        var goodsReceipt = await repository.GetAsync(
            new GoodsReceiptId(command.GoodsReceiptId), cancellationToken);
        if (goodsReceipt is null)
            return Result.Failure(GoodsReceiptErrors.NotFound);

        var result = goodsReceipt.ScanItem(
            command.Sku, command.ActualQty, command.Batch, command.Expiry, command.LineStatus);
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
