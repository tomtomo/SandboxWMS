using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side Product (impl EF di Infrastructure)
// Why: CRUD slice menulis & memuat Product via abstraksi ini tanpa tahu EF (Dependency Inversion,
// FF#5); commit dipisah ke IUnitOfWork. GetAsync mengembalikan aggregate TRACKED → Deactivate/Activate
// ter-persist saat SaveChanges. Get hanya melihat baris aktif (global filter) — cukup untuk lifecycle.
public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    Task<Product?> GetAsync(ProductId id, CancellationToken cancellationToken = default);
}
