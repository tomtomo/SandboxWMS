using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inventory.Application.Features.AdjustStock;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — koreksi manual kuantitas Stock
// Why: mengubah balance fisik secara manual (cycle count / koreksi) adalah operasi sensitif yang WAJIB
// ter-audit (opt-in eksplisit IAuditableCommand, jejak forensik Type/Id — siapa mengoreksi Stock mana).
// AggregateType literal "Stock" (bukan nameof Domain) menjaga command-DTO ringan.
public sealed record AdjustStockCommand(Guid StockId, int NewQty)
    : ICommand, IAuditableCommand
{
    public string AggregateType => "Stock";

    public string AggregateId => StockId.ToString();
}
