using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.MasterData.Application.Features.DeactivateLocation;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — soft-delete Location (set inactive)
public sealed record DeactivateLocationCommand(Guid LocationId) : ICommand, IAuditableCommand
{
    public string AggregateType => "Location";

    public string AggregateId => LocationId.ToString();
}
