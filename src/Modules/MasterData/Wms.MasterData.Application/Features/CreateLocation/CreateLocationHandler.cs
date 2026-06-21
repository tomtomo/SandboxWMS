using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Features.CreateLocation;

// What: CQRS — Command Handler (MediatR) — buka Location master via aggregate (ADR-0004)
// How: factory Create (warehouseId di-wrap strongly-typed) → persist → SaveChanges; kembalikan id.
public sealed class CreateLocationHandler(ILocationRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateLocationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateLocationCommand command, CancellationToken cancellationToken)
    {
        var result = Location.Create(
            LocationId.New(), new WarehouseId(command.WarehouseId), command.Type, command.Code);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await repository.AddAsync(result.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(result.Value.Id.Value);
    }
}
