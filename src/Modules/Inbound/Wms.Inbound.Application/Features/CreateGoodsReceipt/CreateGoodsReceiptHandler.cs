using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.CreateGoodsReceipt;

// What: CQRS Command Handler (ADR-0004) — write path lewat aggregate
// Why: satu use-case self-contained (vertical slice); satu-satunya tempat yang membuat
// GoodsReceipt. Belum MediatR (markers only) — di-invoke langsung endpoint (Phase 02a
// memasang pipeline). Tak emit event (Create bukan fakta yang dipublish, ADR-0026).
// How: factory Create (invariant → Result) → persist via repository port → commit via
// IUnitOfWork.
public sealed class CreateGoodsReceiptHandler(
    IGoodsReceiptRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<Result<Guid>> HandleAsync(
        CreateGoodsReceiptCommand command, CancellationToken cancellationToken = default)
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
