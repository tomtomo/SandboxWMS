using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inbound.Application.Features.ConfirmGoodsReceipt;

// What: CQRS Command (ADR-0004) — konfirmasi GoodsReceipt → memicu GRConfirmed
public sealed record ConfirmGoodsReceiptCommand(Guid GoodsReceiptId) : ICommand;
