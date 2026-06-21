using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inbound.Application.Features.ConfirmGoodsReceipt;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — konfirmasi GoodsReceipt → GRConfirmed
// Why: post-GR adalah tindakan sensitif yang WAJIB ter-audit (ADR-0022) — opt-in EKSPLISIT via
// IAuditableCommand, menyuplai identitas aggregate (Type/Id) untuk jejak forensik, bukan
// reflection. AggregateType literal (bukan nameof Domain) menjaga command-DTO tetap ringan.
public sealed record ConfirmGoodsReceiptCommand(Guid GoodsReceiptId) : ICommand, IAuditableCommand
{
    public string AggregateType => "GoodsReceipt";

    public string AggregateId => GoodsReceiptId.ToString();
}
