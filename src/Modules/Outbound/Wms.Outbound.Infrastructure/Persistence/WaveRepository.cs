using Microsoft.EntityFrameworkCore;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Wave (DDD; ADR-0010)
// How: query tracked agar transisi (AttachPickingTasks/MarkReady/Dispatch) ter-flush saat SaveChanges.
internal sealed class WaveRepository(OutboundDbContext db) : IWaveRepository
{
    public Task AddAsync(Wave wave, CancellationToken cancellationToken = default)
    {
        db.Waves.Add(wave);
        return Task.CompletedTask;
    }

    public Task<Wave?> GetAsync(WaveId id, CancellationToken cancellationToken = default)
        => db.Waves.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
}
