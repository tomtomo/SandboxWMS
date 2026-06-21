using Microsoft.EntityFrameworkCore;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Stock (DDD; ADR-0010)
// How: Add/Remove + query tracked; commit oleh IUnitOfWork. Query mengembalikan entity tracked agar
// transisi state (Allocate/Pick/Putaway) ter-flush saat SaveChanges (tanpa Update eksplisit).
internal sealed class StockRepository(InventoryDbContext db) : IStockRepository
{
    public Task AddAsync(Stock stock, CancellationToken cancellationToken = default)
    {
        db.Stocks.Add(stock);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Stock>> ListAvailableBySkusAsync(
        IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default)
        => await db.Stocks
            .Where(s => s.Status == StockStatus.Available && skus.Contains(s.Sku))
            .ToListAsync(cancellationToken);

    public Task<Stock?> GetAsync(StockId id, CancellationToken cancellationToken = default)
        => db.Stocks.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Stock>> ListPickedByWaveAsync(
        Guid waveId, CancellationToken cancellationToken = default)
        => await db.Stocks
            .Where(s => s.Status == StockStatus.Picked && s.AllocatedToWaveId == waveId)
            .ToListAsync(cancellationToken);

    public void Remove(Stock stock) => db.Stocks.Remove(stock);
}
