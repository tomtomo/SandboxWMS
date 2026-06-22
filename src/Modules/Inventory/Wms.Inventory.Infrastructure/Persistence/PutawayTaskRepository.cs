using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk PutawayTask — Add/GetById dari EfRepository
internal sealed class PutawayTaskRepository(InventoryDbContext db)
    : EfRepository<PutawayTask, PutawayTaskId, InventoryDbContext>(db), IPutawayTaskRepository;
