using Microsoft.EntityFrameworkCore;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk OutboundOrder (DDD; ADR-0010)
// How: Add + query tracked; commit oleh IUnitOfWork. Query mengembalikan entity tracked agar transisi
// (PlaceInWave/Close) ter-flush saat SaveChanges tanpa Update eksplisit. OrderLines (owned) auto-included.
internal sealed class OutboundOrderRepository(OutboundDbContext db) : IOutboundOrderRepository
{
    public Task AddAsync(OutboundOrder order, CancellationToken cancellationToken = default)
    {
        db.OutboundOrders.Add(order);
        return Task.CompletedTask;
    }

    public Task<OutboundOrder?> GetAsync(OutboundOrderId id, CancellationToken cancellationToken = default)
        => db.OutboundOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OutboundOrder>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var typedIds = ids.Select(id => new OutboundOrderId(id)).ToList();
        return await db.OutboundOrders
            .Where(o => typedIds.Contains(o.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboundOrder>> ListByWaveAsync(
        Guid waveId, CancellationToken cancellationToken = default)
        => await db.OutboundOrders
            .Where(o => o.WaveId == waveId)
            .ToListAsync(cancellationToken);
}
