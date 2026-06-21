using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Inbound.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026) untuk aggregate GRAttachment
// Why: identitas surrogate terpisah dari GoodsReceiptId — compiler menolak id-mixup antar aggregate.
public sealed record GRAttachmentId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static GRAttachmentId New() => new(Guid.NewGuid());
}
