using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Abstractions;

// What: Repository Pattern (DDD) — port Stock (impl EF di Infrastructure)
// Why: consumer menulis Stock via abstraksi ini, tak tahu EF (Dependency Inversion,
// FF#5); commit dipisah ke IUnitOfWork (satu transaksi dengan Inbox mark).
public interface IStockRepository
{
    Task AddAsync(Stock stock, CancellationToken cancellationToken = default);
}
