using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inbound.Application.Features.CreateGoodsReceipt;

// What: CQRS Command (ADR-0004) — buka header GoodsReceipt baru (state InProgress)
// Why: write-intent eksplisit; expectedLines = snapshot PO (sku/expectedQty). uom TIDAK lagi disuplai
// caller — di-snapshot dari MasterData read-API (ADR-0014/0011) di handler (gRPC), supaya makna dokumen
// historis stabil walau Product master berubah. poRef/supplierId/dockDoor = metadata header.
public sealed record CreateGoodsReceiptCommand(
    string WarehouseId,
    IReadOnlyList<CreateGoodsReceiptLine> ExpectedLines,
    string? PoRef = null,
    string? SupplierId = null,
    string? DockDoor = null) : ICommand<Guid>;

public sealed record CreateGoodsReceiptLine(string Sku, int ExpectedQty);
