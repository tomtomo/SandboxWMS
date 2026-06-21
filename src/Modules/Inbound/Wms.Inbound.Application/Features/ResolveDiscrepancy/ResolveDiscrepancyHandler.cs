using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.ResolveDiscrepancy;

// What: CQRS — Command Handler (MediatR) — set resolusi pada satu discrepancy (Pending)
public sealed class ResolveDiscrepancyHandler(IGoodsReceiptRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<ResolveDiscrepancyCommand, Result>
{
    public async Task<Result> Handle(ResolveDiscrepancyCommand command, CancellationToken cancellationToken)
    {
        var goodsReceipt = await repository.GetAsync(
            new GoodsReceiptId(command.GoodsReceiptId), cancellationToken);
        if (goodsReceipt is null)
            return Result.Failure(GoodsReceiptErrors.NotFound);

        var result = goodsReceipt.ResolveDiscrepancy(command.Sku, command.Type, command.Action, command.Note);
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
