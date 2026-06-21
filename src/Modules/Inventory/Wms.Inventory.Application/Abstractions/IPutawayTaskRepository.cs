using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Abstractions;

// What: Repository Pattern (DDD) — port PutawayTask (impl EF di Infrastructure)
public interface IPutawayTaskRepository
{
    Task AddAsync(PutawayTask putawayTask, CancellationToken cancellationToken = default);

    // What: ambil satu PutawayTask by id (CompletePutaway slice REST)
    Task<PutawayTask?> GetAsync(PutawayTaskId id, CancellationToken cancellationToken = default);
}
