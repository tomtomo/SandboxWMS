using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.CreateGoodsReceipt;

// What: CQRS — Command Handler (MediatR) — write path lewat aggregate (ADR-0004)
// Why: satu use-case self-contained (vertical slice); satu-satunya tempat yang membuat
// GoodsReceipt. Mengembalikan Result (no-throw-for-business, ADR-0019); transaksi & rollback
// dikelola TransactionBehavior (pipeline), bukan handler. Tak emit event (Create bukan fakta
// yang dipublish, ADR-0026).
// How: factory Create (invariant → Result) → persist via repository port → SaveChanges
// (commit di-finalize TransactionBehavior).
public sealed class CreateGoodsReceiptHandler(
    IGoodsReceiptRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateGoodsReceiptCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateGoodsReceiptCommand command, CancellationToken cancellationToken)
    {
        var lines = command.Lines
            .Select(line => new GoodsReceiptLineInput(line.Sku, line.Quantity))
            .ToList();

        var result = GoodsReceipt.Create(GoodsReceiptId.New(), command.WarehouseId, lines);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await repository.AddAsync(result.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(result.Value.Id.Value);
    }
}
