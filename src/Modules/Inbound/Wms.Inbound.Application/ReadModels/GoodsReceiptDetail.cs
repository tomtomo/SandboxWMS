namespace Wms.Inbound.Application.ReadModels;

// What: read DTO detail (CQRS read-side; ADR-0004) — proyeksi lengkap satu GoodsReceipt untuk detail UI.
// Why: decoupled dari aggregate — Status di-flatten ke string, strongly-typed id → Guid, dan tiga owned
// collection (expected/scanned/discrepancy) di-proyeksikan jadi DTO datar yang stabil terhadap kontrak WebUI.
// Discrepancy.Qty TIDAK ada di domain (Discrepancy hanya Sku/Type/Action/Note) — di-DERIVE deterministik:
// ShortDelivery/OverDelivery dari magnitude variance QuantityCheck per SKU; QcHold/WrongItem dari scanned qty
// (LineStatus cocok) per SKU. Read-side bebas membentuk turunan ini tanpa mengubah invariant aggregate.
public sealed record GoodsReceiptDetail(
    Guid GoodsReceiptId,
    string? PoRef,
    string? SupplierId,
    string? DockDoor,
    string WarehouseId,
    string Status,
    string? HoldReason,
    IReadOnlyList<ExpectedLineDetail> ExpectedLines,
    IReadOnlyList<ScannedLineDetail> ScannedLines,
    IReadOnlyList<DiscrepancyDetail> Discrepancies);

public sealed record ExpectedLineDetail(string Sku, int ExpectedQty, string Uom);

public sealed record ScannedLineDetail(string Sku, int ActualQty, string? Batch, DateOnly? Expiry, string LineStatus);

public sealed record DiscrepancyDetail(string Sku, string Type, int Qty, string? ResolutionAction, string? ResolutionNote);
