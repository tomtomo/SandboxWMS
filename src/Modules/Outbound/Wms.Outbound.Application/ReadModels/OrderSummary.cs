namespace Wms.Outbound.Application.ReadModels;

// What: read DTO (CQRS read-side; ADR-0004) — ringkasan OutboundOrder untuk list UI,
// decoupled dari aggregate: Status di-flatten ke string, owned OrderLines di-ringkas jadi
// LineCount + TotalQty (dibaca in-memory setelah materialisasi).
public sealed record OrderSummary(
    Guid OrderId,
    string CustomerId,
    string ShipTo,
    string Status,
    int LineCount,
    int TotalQty);
