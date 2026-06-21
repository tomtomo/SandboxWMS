using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side Location (impl EF di Infrastructure)
public interface ILocationRepository
{
    Task AddAsync(Location location, CancellationToken cancellationToken = default);

    Task<Location?> GetAsync(LocationId id, CancellationToken cancellationToken = default);
}
