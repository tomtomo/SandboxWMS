using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Product (DDD; ADR-0010)
// Why: Add/GetById dari EfRepository. GetById pakai FirstOrDefault+predicate (bukan Find) → global
// soft-delete filter tetap berlaku (hanya Product aktif; cukup untuk lifecycle Deactivate).
internal sealed class ProductRepository(MasterDataDbContext db)
    : EfRepository<Product, ProductId, MasterDataDbContext>(db), IProductRepository;
