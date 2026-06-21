using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Inventory.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
public sealed record PutawayTaskId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static PutawayTaskId New() => new(Guid.NewGuid());
}
