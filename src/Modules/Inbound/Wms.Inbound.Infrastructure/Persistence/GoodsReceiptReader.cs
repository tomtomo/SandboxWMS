using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Application.ReadModels;
using Wms.Inbound.Domain;

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

    // What: detail read-side (reader-delegation; ADR-0011) — realisasi IGoodsReceiptReader.GetByIdAsync.
    // Why: endpoint REST tak menyentuh DbContext (FF#8); AsNoTracking (read murni). EF mem-load owned
    // collections (expected/scanned/discrepancies) bersama root (owned = bagian aggregate, ikut materialisasi).
    // Materialize-then-map in-memory (enum→string & derived discrepancy Qty bebas batasan translasi LINQ).
    // How: FirstOrDefault by strongly-typed id → null bila absent → map 3 collection + flatten enum.
    public async Task<GoodsReceiptDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var goodsReceipt = await db.GoodsReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(gr => gr.Id == new GoodsReceiptId(id), cancellationToken);

        if (goodsReceipt is null)
            return null;

        var expectedLines = goodsReceipt.ExpectedLines
            .Select(line => new ExpectedLineDetail(line.Sku, line.ExpectedQty, line.Uom))
            .ToList();

        var scannedLines = goodsReceipt.ScannedLines
            .Select(line => new ScannedLineDetail(
                line.Sku, line.ActualQty, line.Batch, line.Expiry, line.LineStatus.ToString()))
            .ToList();

        var discrepancies = goodsReceipt.Discrepancies
            .Select(discrepancy => new DiscrepancyDetail(
                discrepancy.Sku,
                discrepancy.Type.ToString(),
                DeriveQty(goodsReceipt, discrepancy),
                discrepancy.Action?.ToString(),
                discrepancy.Note))
            .ToList();

        return new GoodsReceiptDetail(
            goodsReceipt.Id.Value,
            goodsReceipt.PoRef,
            goodsReceipt.SupplierId,
            goodsReceipt.DockDoor,
            goodsReceipt.WarehouseId,
            goodsReceipt.Status.ToString(),
            goodsReceipt.HoldReason,
            expectedLines,
            scannedLines,
            discrepancies);
    }

    // What: turunan Qty untuk DiscrepancyDetail (DETERMINISTIK) — Discrepancy domain TIDAK punya Qty.
    // Why: kontrak WebUI mengharapkan Qty per discrepancy; di-derive read-side dari sumbu yang sama
    // yang membentuk discrepancy itu (ADR-0013): sumbu KUANTITAS (Short/Over) → magnitude variance dari
    // QuantityCheck per SKU; sumbu KONDISI (QcHold/WrongItem) → scanned qty SKU itu untuk LineStatus cocok.
    private static int DeriveQty(GoodsReceipt goodsReceipt, Discrepancy discrepancy) => discrepancy.Type switch
    {
        DiscrepancyType.ShortDelivery or DiscrepancyType.OverDelivery =>
            goodsReceipt.QuantityChecks
                .Where(check => check.Sku == discrepancy.Sku)
                .Select(check => Math.Abs(check.ReceivedQty - check.ExpectedQty))
                .FirstOrDefault(),
        DiscrepancyType.QcHold =>
            ScannedQtyFor(goodsReceipt, discrepancy.Sku, LineStatus.QcHold),
        DiscrepancyType.WrongItem =>
            ScannedQtyFor(goodsReceipt, discrepancy.Sku, LineStatus.WrongItem),
        _ => 0
    };

    private static int ScannedQtyFor(GoodsReceipt goodsReceipt, string sku, LineStatus lineStatus) =>
        goodsReceipt.ScannedLines
            .Where(line => line.Sku == sku && line.LineStatus == lineStatus)
            .Sum(line => line.ActualQty);
}
