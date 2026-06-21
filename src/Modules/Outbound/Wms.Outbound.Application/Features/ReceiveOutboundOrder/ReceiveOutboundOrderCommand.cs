using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Outbound.Application.Features.ReceiveOutboundOrder;

// What: CQRS Command (ADR-0004) — terima order pengiriman dari sistem eksternal (overview §C1)
// Why: order masuk WMS sebagai New dengan orderLines di-snapshot. Bukan tindakan operator sensitif (datang
// dari sistem eksternal, tak ada di permission catalog overview §E) → BUKAN IAuditableCommand. Mengembalikan
// id order baru (server-generated) via Result<Guid>. uom di-snapshot dari seed (ADR-0014) di handler.
public sealed record ReceiveOutboundOrderCommand(
    string CustomerId,
    string ShipTo,
    IReadOnlyList<ReceiveOrderLine> Lines) : ICommand<Guid>;

// What: satu line order dari request (sku + qty); uom di-snapshot dari seed master data di handler
public sealed record ReceiveOrderLine(string Sku, int Qty);
