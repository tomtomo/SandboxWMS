using Microsoft.EntityFrameworkCore;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Warehouse (DDD; ADR-0010)
// How: Add/Get tracked; commit oleh IUnitOfWork. Get tunduk global soft-delete filter (aktif saja).
internal sealed class WarehouseRepository(MasterDataDbContext db) : IWarehouseRepository
{
    public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        db.Warehouses.Add(warehouse);
        return Task.CompletedTask;
    }

    public Task<Warehouse?> GetAsync(WarehouseId id, CancellationToken cancellationToken = default)
        => db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
}
