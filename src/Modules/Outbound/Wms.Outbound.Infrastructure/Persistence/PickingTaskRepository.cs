using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk PickingTask (DDD; ADR-0010)
// Why: Add/GetById dari EfRepository. ListByWave mendukung gate Wave→Ready (task tracked = status terbaru).
internal sealed class PickingTaskRepository(OutboundDbContext db)
    : EfRepository<PickingTask, PickingTaskId, OutboundDbContext>(db), IPickingTaskRepository
{
    public async Task<IReadOnlyList<PickingTask>> ListByWaveAsync(
        Guid waveId, CancellationToken cancellationToken = default)
        => await DbSet
            .Where(t => t.WaveId == waveId)
            .ToListAsync(cancellationToken);
}
