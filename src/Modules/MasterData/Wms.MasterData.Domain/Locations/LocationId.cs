using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.MasterData.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
// Why: identitas surrogate Location — cegah id-mixup antar aggregate master.
public sealed record LocationId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static LocationId New() => new(Guid.NewGuid());
}
