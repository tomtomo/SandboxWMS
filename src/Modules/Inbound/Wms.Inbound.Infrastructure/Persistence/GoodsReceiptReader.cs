using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Application.ReadModels;

namespace Wms.Inbound.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — realisasi IGoodsReceiptReader
// Why: endpoint REST (*.Api) tak menyentuh DbContext (FF#8) — query list dilayani di sini, AsNoTracking
// (read murni), paginated (Skip/Take) + TotalCount (CountAsync) atas FILTER yang SAMA. Materialize-then-map
// (Status enum→string & owned ExpectedLines.Count in-memory, bebas batasan translasi owned/strongly-typed id).
// How: clamp page/pageSize (guard) → Count → OrderBy CreatedAt desc → Skip/Take → map → PagedResult.
internal sealed class GoodsReceiptReader(InboundDbContext db) : IGoodsReceiptReader
{
    public async Task<PagedResult<GoodsReceiptListItem>> ListAsync(
        string? warehouseId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeSize) = PageRequest.From(page, pageSize);

        var query = db.GoodsReceipts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(warehouseId))
            query = query.Where(goodsReceipt => goodsReceipt.WarehouseId == warehouseId);

        var totalCount = await query.CountAsync(cancellationToken);

        var goodsReceipts = await query
            .OrderByDescending(goodsReceipt => goodsReceipt.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        var items = goodsReceipts
            .Select(goodsReceipt => new GoodsReceiptListItem(
                goodsReceipt.Id.Value,
                goodsReceipt.WarehouseId,
                goodsReceipt.PoRef,
                goodsReceipt.SupplierId,
                goodsReceipt.Status.ToString(),
                goodsReceipt.ExpectedLines.Count,
                goodsReceipt.CreatedAt))
            .ToList();

        return new PagedResult<GoodsReceiptListItem>(items, safePage, safeSize, totalCount);
    }
}
