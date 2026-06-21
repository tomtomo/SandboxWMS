using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inbound.Application.Features.HoldGoodsReceipt;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — SPV reject seluruh GR (Pending→Hold)
// Why: penolakan GR (alasan berat) adalah keputusan sensitif yang WAJIB ter-audit. TIDAK emit event.
public sealed record HoldGoodsReceiptCommand(Guid GoodsReceiptId, string Reason)
    : ICommand, IAuditableCommand
{
    public string AggregateType => "GoodsReceipt";

    public string AggregateId => GoodsReceiptId.ToString();
}
