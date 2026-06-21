using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Outbound.Application.Features.CompletePicking;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — operator selesaikan picking
// Why: memindah Stock dari rak ke staging (Allocated→Picked di Inventory) mengubah ketersediaan stok →
// tindakan WAJIB ter-audit (opt-in eksplisit IAuditableCommand, jejak forensik Type/Id). AggregateId =
// pickingTaskId (dikenal dari route, beda dgn create yang server-generated). AggregateType literal ringan.
public sealed record CompletePickingCommand(Guid PickingTaskId, string StagingLocationId)
    : ICommand, IAuditableCommand
{
    public string AggregateType => "PickingTask";

    public string AggregateId => PickingTaskId.ToString();
}
