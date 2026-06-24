using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Application.ReadModels;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — realisasi IStockReader
// Why: endpoint REST (*.Api) tak menyentuh DbContext (FF#8) — query list dilayani di sini, AsNoTracking
// (read murni), paginated (Skip/Take) + TotalCount (CountAsync) atas FILTER yang SAMA. Materialize-then-map
// (Status enum→string & StockId→Guid in-memory, bebas batasan translasi strongly-typed id).
// How: clamp page/pageSize (guard) → 3 conditional Where → Count → OrderBy → Skip/Take → map → PagedResult.
internal sealed class StockReader(InventoryDbContext db) : IStockReader
{
    public async Task<PagedResult<StockListItem>> ListAsync(
        string? warehouseId = null,
        string? sku = null,
        StockStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeSize) = PageRequest.From(page, pageSize);

        var query = db.Stocks.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(warehouseId))
            query = query.Where(stock => stock.WarehouseId == warehouseId);
        if (!string.IsNullOrWhiteSpace(sku))
            query = query.Where(stock => stock.Sku == sku);
        if (status is not null)
            query = query.Where(stock => stock.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var stocks = await query
            .OrderBy(stock => stock.Sku)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        var items = stocks
            .Select(stock => new StockListItem(
                stock.Id.Value,
                stock.WarehouseId,
                stock.Sku,
                stock.LocationId,
                stock.Batch,
                stock.Expiry,
                stock.Quantity,
                stock.Status.ToString(),
                stock.SourceGoodsReceiptId,
                stock.AllocatedToWaveId,
                stock.PickingTaskId))
            .ToList();

        return new PagedResult<StockListItem>(items, safePage, safeSize, totalCount);
    }
}
