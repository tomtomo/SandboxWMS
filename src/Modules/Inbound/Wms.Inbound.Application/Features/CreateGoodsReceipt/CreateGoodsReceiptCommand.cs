using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inbound.Application.Features.CreateGoodsReceipt;

// What: CQRS Command (ADR-0004) — buka header GoodsReceipt baru (state InProgress)
// Why: write-intent eksplisit; expectedLines = snapshot PO (sku/expectedQty/uom) — uom dibekukan
// per ADR-0014 (stand-in PO/seed sampai MasterData 04a). poRef/supplierId/dockDoor = metadata header.
public sealed record CreateGoodsReceiptCommand(
    string WarehouseId,
    IReadOnlyList<CreateGoodsReceiptLine> ExpectedLines,
    string? PoRef = null,
    string? SupplierId = null,
    string? DockDoor = null) : ICommand<Guid>;

public sealed record CreateGoodsReceiptLine(string Sku, int ExpectedQty, string Uom);
