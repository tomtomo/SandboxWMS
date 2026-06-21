using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Features.CreateWarehouse;

// What: CQRS — Command Handler (MediatR) — buka Warehouse master via aggregate (ADR-0004)
// How: factory Create (id surrogate di-generate) → persist → SaveChanges; kembalikan id.
public sealed class CreateWarehouseHandler(IWarehouseRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateWarehouseCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        var result = Warehouse.Create(WarehouseId.New(), command.Name, command.Address);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await repository.AddAsync(result.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(result.Value.Id.Value);
    }
}
