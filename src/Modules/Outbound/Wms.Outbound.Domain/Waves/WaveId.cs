using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Outbound.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
// Why: identitas surrogate Wave — cegah id-mixup antar aggregate.
public sealed record WaveId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static WaveId New() => new(Guid.NewGuid());
}
