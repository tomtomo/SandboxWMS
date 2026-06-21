using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.ResolveDiscrepancy;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — SPV resolve satu discrepancy (Pending)
// Why: keputusan SPV yang menentukan turunan received/rejected payload → tindakan sensitif yang
// WAJIB ter-audit (opt-in eksplisit IAuditableCommand, jejak forensik Type/Id).
public sealed record ResolveDiscrepancyCommand(
    Guid GoodsReceiptId,
    string Sku,
    DiscrepancyType Type,
    ResolutionAction Action,
    string? Note = null) : ICommand, IAuditableCommand
{
    public string AggregateType => "GoodsReceipt";

    public string AggregateId => GoodsReceiptId.ToString();
}
