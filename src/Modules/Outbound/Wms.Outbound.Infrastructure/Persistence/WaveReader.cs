using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Application.ReadModels;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — realisasi IWaveReader
// Why: endpoint REST (*.Api) tak menyentuh DbContext (FF#8). Wave mereferensikan order by id (Guid)
// via primitive collection OrderIds (uuid[]) — TIDAK di-query di SQL, dibaca in-memory SETELAH materialisasi.
// OrderCount = OrderIds.Count; LineCount/Lines dirakit dari FOLLOW-UP query OutboundOrders yang Id-nya
// ada di OrderIds wave (cross-aggregate read sah di reader read-side, bukan navigation domain).
// How: list → clamp/Count/OrderBy CreatedAt desc/Skip/Take → per wave hitung LineCount via follow-up query.
// Detail → materialize wave → capture OrderIds → query order-order itu → flatten OrderLines (Id via indeks).
internal sealed class WaveReader(OutboundDbContext db) : IWaveReader
{
    public async Task<PagedResult<WaveSummary>> ListAsync(
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeSize) = PageRequest.From(page, pageSize);

        var query = db.Waves.AsNoTracking();
        if (Enum.TryParse<WaveStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(wave => wave.Status == parsedStatus);

        var totalCount = await query.CountAsync(cancellationToken);

        var waves = await query
            .OrderByDescending(wave => wave.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        var items = new List<WaveSummary>(waves.Count);
        foreach (var wave in waves)
        {
            // OrderIds = primitive collection (in-memory post-materialize); LineCount = follow-up query.
            var orderIds = wave.OrderIds.Select(id => new OutboundOrderId(id)).ToList();
            var lineCount = await db.OutboundOrders
                .AsNoTracking()
                .Where(order => orderIds.Contains(order.Id))
                .SelectMany(order => order.OrderLines)
                .CountAsync(cancellationToken);

            items.Add(new WaveSummary(
                wave.Id.Value,
                wave.Status.ToString(),
                wave.OrderIds.Count,
                lineCount));
        }

        return new PagedResult<WaveSummary>(items, safePage, safeSize, totalCount);
    }

    public async Task<WaveDetail?> GetAsync(Guid waveId, CancellationToken cancellationToken = default)
    {
        var wave = await db.Waves
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == new WaveId(waveId), cancellationToken);

        if (wave is null)
            return null;

        var orderIds = wave.OrderIds.ToList();
        var typedOrderIds = orderIds.Select(id => new OutboundOrderId(id)).ToList();

        var orders = await db.OutboundOrders
            .AsNoTracking()
            .Where(order => typedOrderIds.Contains(order.Id))
            .ToListAsync(cancellationToken);

        // union OrderLines lintas order wave, tiap line di-tag OrderId; Id line = indeks 1-based (display "#").
        var lines = orders
            .SelectMany(order => order.OrderLines.Select(line => (OrderId: order.Id.Value, line.Sku, line.Qty)))
            .Select((entry, index) => new WaveLine(index + 1, entry.OrderId, entry.Sku, entry.Qty))
            .ToList();

        return new WaveDetail(
            wave.Id.Value,
            wave.Status.ToString(),
            orderIds,
            lines);
    }
}
