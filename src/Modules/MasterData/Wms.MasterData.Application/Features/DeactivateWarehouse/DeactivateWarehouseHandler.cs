using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Features.DeactivateWarehouse;

// What: CQRS — Command Handler (MediatR) — soft-delete Warehouse (overview §D, ADR-0014)
public sealed class DeactivateWarehouseHandler(IWarehouseRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateWarehouseCommand, Result>
{
    public async Task<Result> Handle(DeactivateWarehouseCommand command, CancellationToken cancellationToken)
    {
        var warehouse = await repository.GetByIdAsync(new WarehouseId(command.WarehouseId), cancellationToken);
        if (warehouse is null)
            return Result.Failure(WarehouseErrors.NotFound);

        var result = warehouse.Deactivate();
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
