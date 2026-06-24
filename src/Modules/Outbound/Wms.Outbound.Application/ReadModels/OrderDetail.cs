namespace Wms.Outbound.Application.ReadModels;

// What: read DTO detail (CQRS read-side; ADR-0004) — satu OutboundOrder + owned OrderLines,
// untuk halaman detail WebUI. Status di-flatten ke string; Lines = proyeksi OrderLine.
// How: Id line di-proyeksi sebagai indeks 1-based saat mapping (OrderLine tak punya public Id —
// EF memetakan shadow key int "id"; UI hanya merender Id sebagai "#").
public sealed record OrderDetail(
    Guid OrderId,
    string CustomerId,
    string ShipTo,
    string Status,
    IReadOnlyList<OrderLineReadModel> Lines);

// What: satu line dalam OrderDetail — Sku/Qty/Uom di-snapshot; Id = indeks 1-based (display "#").
public sealed record OrderLineReadModel(
    int Id,
    string Sku,
    int Qty,
    string Uom);
