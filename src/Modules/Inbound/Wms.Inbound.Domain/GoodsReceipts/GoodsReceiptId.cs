using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Inbound.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
// Why: identitas surrogate GoodsReceipt — cegah id-mixup dengan id aggregate lain
// (compiler menolak, mis., WarehouseId di slot GoodsReceiptId).
public sealed record GoodsReceiptId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static GoodsReceiptId New() => new(Guid.NewGuid());
}
