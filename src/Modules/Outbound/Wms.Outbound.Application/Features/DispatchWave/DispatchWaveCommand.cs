using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Outbound.Application.Features.DispatchWave;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — SPV dispatch wave (overview §C6)
// Why: dispatch = barang fisik keluar gudang (Stock Picked di-remove di Inventory) → tindakan sensitif WAJIB
// ter-audit (opt-in eksplisit IAuditableCommand, jejak forensik Type/Id). AggregateId = waveId (dari route).
public sealed record DispatchWaveCommand(Guid WaveId) : ICommand, IAuditableCommand
{
    public string AggregateType => "Wave";

    public string AggregateId => WaveId.ToString();
}
