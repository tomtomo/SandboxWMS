using Microsoft.EntityFrameworkCore;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk PickingTask (DDD; ADR-0010)
// How: query tracked agar transisi (Complete) ter-flush saat SaveChanges. ListByWaveAsync mendukung gate
// Wave→Ready (CompletePicking) — task yang baru di-Complete dalam scope sama sudah tracked (status terbaru).
internal sealed class PickingTaskRepository(OutboundDbContext db) : IPickingTaskRepository
{
    public Task AddAsync(PickingTask task, CancellationToken cancellationToken = default)
    {
        db.PickingTasks.Add(task);
        return Task.CompletedTask;
    }

    public Task<PickingTask?> GetAsync(PickingTaskId id, CancellationToken cancellationToken = default)
        => db.PickingTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PickingTask>> ListByWaveAsync(
        Guid waveId, CancellationToken cancellationToken = default)
        => await db.PickingTasks
            .Where(t => t.WaveId == waveId)
            .ToListAsync(cancellationToken);
}
