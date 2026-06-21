using Microsoft.EntityFrameworkCore;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk PutawayTask (DDD; ADR-0010)
internal sealed class PutawayTaskRepository(InventoryDbContext db) : IPutawayTaskRepository
{
    public Task AddAsync(PutawayTask putawayTask, CancellationToken cancellationToken = default)
    {
        db.PutawayTasks.Add(putawayTask);
        return Task.CompletedTask;
    }

    public Task<PutawayTask?> GetAsync(PutawayTaskId id, CancellationToken cancellationToken = default)
        => db.PutawayTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
}
