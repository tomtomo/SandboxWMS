using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port Wave (impl EF di Infrastructure)
public interface IWaveRepository
{
    Task AddAsync(Wave wave, CancellationToken cancellationToken = default);

    // What: ambil satu Wave by id (StockAllocated consumer attach tasks; CompletePicking/DispatchWave)
    Task<Wave?> GetAsync(WaveId id, CancellationToken cancellationToken = default);
}
