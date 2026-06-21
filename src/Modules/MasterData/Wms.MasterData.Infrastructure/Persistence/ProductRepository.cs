using Microsoft.EntityFrameworkCore;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Product (DDD; ADR-0010)
// How: Get tunduk global soft-delete filter (aktif saja) — cukup untuk lifecycle CRUD (Deactivate
// memuat yang aktif). Pembacaan yang harus melihat inactive = jalur read-port (targeted bypass).
internal sealed class ProductRepository(MasterDataDbContext db) : IProductRepository
{
    public Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        db.Products.Add(product);
        return Task.CompletedTask;
    }

    public Task<Product?> GetAsync(ProductId id, CancellationToken cancellationToken = default)
        => db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
}
