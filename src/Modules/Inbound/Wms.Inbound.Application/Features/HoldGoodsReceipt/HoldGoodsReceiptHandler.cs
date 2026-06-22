using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.HoldGoodsReceipt;

// What: CQRS — Command Handler (MediatR) — transisi Pending→Hold (tanpa emit event)
public sealed class HoldGoodsReceiptHandler(IGoodsReceiptRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<HoldGoodsReceiptCommand, Result>
{
    public async Task<Result> Handle(HoldGoodsReceiptCommand command, CancellationToken cancellationToken)
    {
        var goodsReceipt = await repository.GetByIdAsync(
            new GoodsReceiptId(command.GoodsReceiptId), cancellationToken);
        if (goodsReceipt is null)
            return Result.Failure(GoodsReceiptErrors.NotFound);

        var result = goodsReceipt.Hold(command.Reason);
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
