using Wms.Inbound.Application.ReadModels;

namespace Wms.Inbound.Application.Abstractions;

// What: Read-Port (CQRS read-side + Ports & Adapters; ADR-0004/0011) — sisi-baca GoodsReceipt
// Why: query list mem-bypass aggregate/repository (baca langsung → read-DTO) sesuai CQRS; endpoint
// REST (*.Api) bergantung ke abstraksi ini, BUKAN DbContext (FF#8) — impl EF di Infrastructure.
// How: dipanggil ListGoodsReceiptsEndpoint; warehouseId optional sebagai filter scoping warehouse.
public interface IGoodsReceiptReader
{
    Task<IReadOnlyList<GoodsReceiptListItem>> ListAsync(
        string? warehouseId = null, CancellationToken cancellationToken = default);
}
