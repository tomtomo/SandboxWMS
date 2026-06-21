using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inbound.Application.Features.CreateGoodsReceipt;

// What: CQRS Command (ADR-0004) — buat GoodsReceipt baru (state InProgress)
// Why: write-intent eksplisit; marker ICommand<Guid> dipakai TransactionBehavior
// (Phase 02a) untuk membuka transaksi hanya di sisi command.
public sealed record CreateGoodsReceiptCommand(
    string WarehouseId,
    IReadOnlyList<CreateGoodsReceiptLine> Lines) : ICommand<Guid>;

public sealed record CreateGoodsReceiptLine(string Sku, int Quantity);
