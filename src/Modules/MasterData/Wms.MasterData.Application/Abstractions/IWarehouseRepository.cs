using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side Warehouse (impl EF di Infrastructure)
public interface IWarehouseRepository
{
    Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default);

    Task<Warehouse?> GetAsync(WarehouseId id, CancellationToken cancellationToken = default);
}
