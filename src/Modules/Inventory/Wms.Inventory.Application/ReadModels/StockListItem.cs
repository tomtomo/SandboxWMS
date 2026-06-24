namespace Wms.Inventory.Application.ReadModels;

// What: read DTO (CQRS read-side; ADR-0004) — ringkasan Stock untuk list UI (WebUI maturity),
// decoupled dari aggregate: Status di-flatten ke string, StockId di-flatten ke Guid (.Value).
// Why: endpoint REST tak mem-bocorkan tipe Domain (StockId/StockStatus) ke kontrak; field
// strongly-typed/enum dipetakan ke primitif yang stabil di seam JSON (string-enum via host).
public sealed record StockListItem(
    Guid StockId,
    string WarehouseId,
    string Sku,
    string LocationId,
    string? Batch,
    DateOnly? Expiry,
    int Quantity,
    string Status,
    Guid SourceGoodsReceiptId,
    Guid? AllocatedToWaveId,
    Guid? PickingTaskId);
