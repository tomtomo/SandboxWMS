using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inbound.Application.Features.DeclareScanComplete;

// What: CQRS Command (ADR-0004) — operator declare scan selesai (InProgress→Pending)
// Why: memicu kompilasi discrepancy dua-sumbu (ADR-0013) di aggregate.
public sealed record DeclareScanCompleteCommand(Guid GoodsReceiptId) : ICommand;
