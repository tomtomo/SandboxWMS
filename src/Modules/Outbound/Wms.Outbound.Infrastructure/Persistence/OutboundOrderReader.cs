using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Application.ReadModels;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — realisasi IOutboundOrderReader
// Why: endpoint REST (*.Api) tak menyentuh DbContext (FF#8) — query list/detail dilayani di sini,
// AsNoTracking (read murni), list paginated (Skip/Take) + TotalCount atas FILTER yang SAMA.
// Materialize-then-map (Status enum→string; owned OrderLines Count/Sum in-memory, bebas batasan translasi).
// How: status string di-parse ke OutboundOrderStatus (kolom disimpan sebagai enum-NAME) → filter; clamp
// page/pageSize → Count → OrderBy CreatedAt desc → Skip/Take → map → PagedResult. Detail: FirstOrDefault by id.
internal sealed class OutboundOrderReader(OutboundDbContext db) : IOutboundOrderReader
{
    public async Task<PagedResult<OrderSummary>> ListAsync(
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeSize) = PageRequest.From(page, pageSize);

        var query = db.OutboundOrders.AsNoTracking();
        if (Enum.TryParse<OutboundOrderStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(order => order.Status == parsedStatus);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .OrderByDescending(order => order.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        var items = orders
            .Select(order => new OrderSummary(
                order.Id.Value,
                order.CustomerId,
                order.ShipTo,
                order.Status.ToString(),
                order.OrderLines.Count,
                order.OrderLines.Sum(line => line.Qty)))
            .ToList();

        return new PagedResult<OrderSummary>(items, safePage, safeSize, totalCount);
    }

    public async Task<OrderDetail?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.OutboundOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == new OutboundOrderId(orderId), cancellationToken);

        if (order is null)
            return null;

        // Id line = indeks 1-based (OrderLine tak punya public Id; UI hanya render "#") — map in-memory.
        var lines = order.OrderLines
            .Select((line, index) => new OrderLineReadModel(index + 1, line.Sku, line.Qty, line.Uom))
            .ToList();

        return new OrderDetail(
            order.Id.Value,
            order.CustomerId,
            order.ShipTo,
            order.Status.ToString(),
            lines);
    }
}
