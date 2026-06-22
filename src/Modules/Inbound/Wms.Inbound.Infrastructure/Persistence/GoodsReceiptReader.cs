using Microsoft.EntityFrameworkCore;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Application.ReadModels;

namespace Wms.Inbound.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — realisasi IGoodsReceiptReader
// Why: endpoint REST (*.Api) tak menyentuh DbContext (FF#8) — query list dilayani di sini, AsNoTracking
// (read murni). Materialize-then-map (bukan projection EF): Status enum→string & owned ExpectedLines.Count
// dihitung in-memory, bebas batasan translasi owned-collection/strongly-typed id (pola MasterDataReader).
internal sealed class GoodsReceiptReader(InboundDbContext db) : IGoodsReceiptReader
{
    public async Task<IReadOnlyList<GoodsReceiptListItem>> ListAsync(
        string? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var query = db.GoodsReceipts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(warehouseId))
            query = query.Where(goodsReceipt => goodsReceipt.WarehouseId == warehouseId);

        var goodsReceipts = await query
            .OrderByDescending(goodsReceipt => goodsReceipt.CreatedAt)
            .ToListAsync(cancellationToken);

        return goodsReceipts
            .Select(goodsReceipt => new GoodsReceiptListItem(
                goodsReceipt.Id.Value,
                goodsReceipt.WarehouseId,
                goodsReceipt.PoRef,
                goodsReceipt.SupplierId,
                goodsReceipt.Status.ToString(),
                goodsReceipt.ExpectedLines.Count,
                goodsReceipt.CreatedAt))
            .ToList();
    }
}
