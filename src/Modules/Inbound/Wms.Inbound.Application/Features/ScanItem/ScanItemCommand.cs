using Wms.BuildingBlocks.Application.Messaging;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.ScanItem;

// What: CQRS Command (ADR-0004) — operator scan satu carton/line (state tetap InProgress)
// Why: write-intent eksplisit; LineStatus (sumbu kondisi two-axis) di-tag operator saat scan.
public sealed record ScanItemCommand(
    Guid GoodsReceiptId,
    string Sku,
    int ActualQty,
    string? Batch,
    DateOnly? Expiry,
    LineStatus LineStatus) : ICommand;
