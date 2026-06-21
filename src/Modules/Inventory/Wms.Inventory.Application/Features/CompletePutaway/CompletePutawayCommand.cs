using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inventory.Application.Features.CompletePutaway;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — operator selesaikan putaway
// Why: memindah Stock receiving→rak adalah operasi yang mengubah ketersediaan stock (OnHand→Available)
// → tindakan yang WAJIB ter-audit (opt-in eksplisit IAuditableCommand, jejak forensik Type/Id).
// AggregateType literal (bukan nameof Domain) menjaga command-DTO ringan.
public sealed record CompletePutawayCommand(Guid PutawayTaskId, string ActualDestinationId)
    : ICommand, IAuditableCommand
{
    public string AggregateType => "PutawayTask";

    public string AggregateId => PutawayTaskId.ToString();
}
