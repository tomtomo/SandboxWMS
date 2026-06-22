using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Warehouse — Add/GetById dari EfRepository
// Why: GetById tunduk global soft-delete filter (aktif saja) via FirstOrDefault+predicate.
internal sealed class WarehouseRepository(MasterDataDbContext db)
    : EfRepository<Warehouse, WarehouseId, MasterDataDbContext>(db), IWarehouseRepository;
