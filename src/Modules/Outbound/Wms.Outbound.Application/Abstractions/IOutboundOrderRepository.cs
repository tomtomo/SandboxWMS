using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port OutboundOrder; Add/GetById dari IRepository + query domain
public interface IOutboundOrderRepository : IRepository<OutboundOrder, OutboundOrderId>
{
    // What: muat order-order terpilih (CreateWave: SPV pilih beberapa order) — eager load OrderLines
    // Why: handler butuh orderLines untuk komposisi WaveReleased lines[] (cross-aggregate) + transisi New→InProgress
    Task<IReadOnlyList<OutboundOrder>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    // What: semua order terikat ke wave (DispatchWave → Close tiap order)
    Task<IReadOnlyList<OutboundOrder>> ListByWaveAsync(Guid waveId, CancellationToken cancellationToken = default);
}
