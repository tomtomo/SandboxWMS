using Wms.BuildingBlocks.Application.Pagination;
using Wms.Inbound.Application.ReadModels;

namespace Wms.Inbound.Application.Abstractions;

// What: Read-Port (CQRS read-side + Ports & Adapters; ADR-0004/0011) — sisi-baca GoodsReceipt
// Why: query list mem-bypass aggregate/repository (baca langsung → read-DTO) sesuai CQRS; endpoint
// REST (*.Api) bergantung ke abstraksi ini, BUKAN DbContext (FF#8) — impl EF di Infrastructure.
// Paginated (PagedResult) — cegah unbounded result set (Nygard, Release It!), seragam lintas modul.
// How: dipanggil ListGoodsReceiptsEndpoint; warehouseId optional filter scoping; page 1-based.
public interface IGoodsReceiptReader
{
    Task<PagedResult<GoodsReceiptListItem>> ListAsync(
        string? warehouseId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
