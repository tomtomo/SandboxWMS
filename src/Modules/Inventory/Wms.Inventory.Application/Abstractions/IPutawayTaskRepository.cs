using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Abstractions;

// What: Repository Pattern (DDD) — port PutawayTask; Add/GetById dari IRepository
public interface IPutawayTaskRepository : IRepository<PutawayTask, PutawayTaskId>
{
}
