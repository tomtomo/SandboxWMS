using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Stock (DDD; ADR-0010)
// Why: Add/GetById dari EfRepository (hapus boilerplate). Query domain (ListAvailable/ListPicked) + Remove
// di sini. Query mengembalikan entity tracked → transisi (Allocate/Pick/Putaway) ter-flush saat SaveChanges.
internal sealed class StockRepository(InventoryDbContext db)
    : EfRepository<Stock, StockId, InventoryDbContext>(db), IStockRepository
{
    public async Task<IReadOnlyList<Stock>> ListAvailableBySkusAsync(
        IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default)
        => await DbSet
            .Where(s => s.Status == StockStatus.Available && skus.Contains(s.Sku))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Stock>> ListPickedByWaveAsync(
        Guid waveId, CancellationToken cancellationToken = default)
        => await DbSet
            .Where(s => s.Status == StockStatus.Picked && s.AllocatedToWaveId == waveId)
            .ToListAsync(cancellationToken);

    public void Remove(Stock stock) => DbSet.Remove(stock);
}
