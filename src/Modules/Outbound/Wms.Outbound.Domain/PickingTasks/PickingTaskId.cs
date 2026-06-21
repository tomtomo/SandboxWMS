using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Outbound.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
public sealed record PickingTaskId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static PickingTaskId New() => new(Guid.NewGuid());
}
