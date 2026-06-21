using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port OutboundOrder (impl EF di Infrastructure)
// Why: handler menulis & meng-query OutboundOrder via abstraksi ini, tak tahu EF (Dependency Inversion,
// FF#5); commit dipisah ke IUnitOfWork. Query mengembalikan aggregate TRACKED → transisi (PlaceInWave/Close)
// ter-persist saat SaveChanges tanpa Update eksplisit.
public interface IOutboundOrderRepository
{
    Task AddAsync(OutboundOrder order, CancellationToken cancellationToken = default);

    Task<OutboundOrder?> GetAsync(OutboundOrderId id, CancellationToken cancellationToken = default);

    // What: muat order-order terpilih (CreateWave: SPV pilih beberapa order) — eager load OrderLines
    // Why: handler butuh orderLines untuk komposisi WaveReleased lines[] (cross-aggregate) + transisi New→InProgress
    Task<IReadOnlyList<OutboundOrder>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    // What: semua order terikat ke wave (DispatchWave → Close tiap order)
    Task<IReadOnlyList<OutboundOrder>> ListByWaveAsync(Guid waveId, CancellationToken cancellationToken = default);
}
