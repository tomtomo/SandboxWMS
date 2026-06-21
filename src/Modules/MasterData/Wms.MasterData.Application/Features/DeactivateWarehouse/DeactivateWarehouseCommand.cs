using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.MasterData.Application.Features.DeactivateWarehouse;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — soft-delete Warehouse (set inactive)
public sealed record DeactivateWarehouseCommand(Guid WarehouseId) : ICommand, IAuditableCommand
{
    public string AggregateType => "Warehouse";

    public string AggregateId => WarehouseId.ToString();
}
