using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Features.DeactivateLocation;

// What: CQRS — Command Handler (MediatR) — soft-delete Location (overview §D, ADR-0014)
public sealed class DeactivateLocationHandler(ILocationRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateLocationCommand, Result>
{
    public async Task<Result> Handle(DeactivateLocationCommand command, CancellationToken cancellationToken)
    {
        var location = await repository.GetByIdAsync(new LocationId(command.LocationId), cancellationToken);
        if (location is null)
            return Result.Failure(LocationErrors.NotFound);

        var result = location.Deactivate();
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
