using Wms.BuildingBlocks.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side Product; Add/GetById dari IRepository
// Why: hapus duplikasi; commit dipisah ke IUnitOfWork. GetById tunduk global soft-delete filter (aktif saja)
// — cukup untuk lifecycle CRUD. Pembacaan inactive/list = jalur read-port (IMasterDataReader). Tanpa query
// write-side tambahan.
public interface IProductRepository : IRepository<Product, ProductId>
{
}
