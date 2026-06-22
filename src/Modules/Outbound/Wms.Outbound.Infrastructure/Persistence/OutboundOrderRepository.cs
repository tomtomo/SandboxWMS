using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk OutboundOrder (DDD; ADR-0010)
// Why: Add/GetById dari EfRepository. Query domain (ListByIds/ListByWave) mengembalikan entity tracked →
// transisi (PlaceInWave/Close) ter-flush saat SaveChanges. OrderLines (owned) auto-included.
internal sealed class OutboundOrderRepository(OutboundDbContext db)
    : EfRepository<OutboundOrder, OutboundOrderId, OutboundDbContext>(db), IOutboundOrderRepository
{
    public async Task<IReadOnlyList<OutboundOrder>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var typedIds = ids.Select(id => new OutboundOrderId(id)).ToList();
        return await DbSet
            .Where(o => typedIds.Contains(o.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboundOrder>> ListByWaveAsync(
        Guid waveId, CancellationToken cancellationToken = default)
        => await DbSet
            .Where(o => o.WaveId == waveId)
            .ToListAsync(cancellationToken);
}
