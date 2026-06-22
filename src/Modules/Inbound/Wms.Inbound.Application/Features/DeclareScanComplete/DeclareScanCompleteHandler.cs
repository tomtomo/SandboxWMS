using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.DeclareScanComplete;

// What: CQRS — Command Handler (MediatR) — transisi InProgress→Pending + kompilasi discrepancy
public sealed class DeclareScanCompleteHandler(IGoodsReceiptRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeclareScanCompleteCommand, Result>
{
    public async Task<Result> Handle(DeclareScanCompleteCommand command, CancellationToken cancellationToken)
    {
        var goodsReceipt = await repository.GetByIdAsync(
            new GoodsReceiptId(command.GoodsReceiptId), cancellationToken);
        if (goodsReceipt is null)
            return Result.Failure(GoodsReceiptErrors.NotFound);

        var result = goodsReceipt.DeclareScanComplete();
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
