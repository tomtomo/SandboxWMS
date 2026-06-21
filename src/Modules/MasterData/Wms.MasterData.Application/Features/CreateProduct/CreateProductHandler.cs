using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Features.CreateProduct;

// What: CQRS — Command Handler (MediatR) — buka Product master via aggregate (ADR-0004)
// Why: satu-satunya tempat yang mengonstruksi Product; factory menegakkan invariant → Result
// (no-throw, ADR-0019); transaksi & rollback dikelola TransactionBehavior. Tak emit event (Create
// bukan fakta yang dipublish; MasterData read-only ke core, ADR-0011/0026).
// How: factory Create → persist via repository port → SaveChanges (commit di-finalize pipeline).
public sealed class CreateProductHandler(IProductRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateProductCommand, Result<string>>
{
    public async Task<Result<string>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var result = Product.Create(
            command.Sku, command.Name, command.Uom, command.BatchTrackingRequired,
            command.ExpiryTrackingRequired, command.QcRequiredOnReceipt, command.ShelfLifeDays);
        if (result.IsFailure)
            return Result.Failure<string>(result.Error);

        await repository.AddAsync(result.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(result.Value.Id.Value);
    }
}
