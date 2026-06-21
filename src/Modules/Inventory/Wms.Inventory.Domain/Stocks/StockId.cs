using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Inventory.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
// Why: identitas surrogate Stock — cegah id-mixup antar aggregate.
public sealed record StockId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static StockId New() => new(Guid.NewGuid());
}
