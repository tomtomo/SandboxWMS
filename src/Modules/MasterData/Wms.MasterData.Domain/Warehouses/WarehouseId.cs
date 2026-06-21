using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.MasterData.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
// Why: identitas surrogate Warehouse — cegah id-mixup antar aggregate master (mis. lempar
// LocationId ke slot WarehouseId; compiler menolak).
public sealed record WarehouseId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static WarehouseId New() => new(Guid.NewGuid());
}
