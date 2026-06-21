using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Stock (DDD; ADR-0010)
// How: Add saja (consumer write-only di 01c); commit oleh IUnitOfWork.
internal sealed class StockRepository(InventoryDbContext db) : IStockRepository
{
    public Task AddAsync(Stock stock, CancellationToken cancellationToken = default)
    {
        db.Stocks.Add(stock);
        return Task.CompletedTask;
    }
}
