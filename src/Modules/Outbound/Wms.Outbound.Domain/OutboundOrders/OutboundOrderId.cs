using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Outbound.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
// Why: identitas surrogate OutboundOrder — cegah id-mixup antar aggregate.
public sealed record OutboundOrderId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static OutboundOrderId New() => new(Guid.NewGuid());
}
