using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Outbound.Domain;

// What: katalog Error domain OutboundOrder (Result pattern, ADR-0019)
// Why: kegagalan bisnis sebagai nilai ber-Code stabil. Input kosong = Validation (400); transisi ilegal
// (state sekarang bentrok dgn operasi diminta) = Conflict (409) — pemetaan otomatis di transport (ADR-0019).
public static class OutboundOrderErrors
{
    public static readonly Error NotFound =
        Error.NotFound("outbound_order.not_found", "OutboundOrder tidak ditemukan.");

    public static readonly Error MissingCustomer =
        Error.Validation("outbound_order.missing_customer", "customerId wajib diisi.");

    public static readonly Error MissingShipTo =
        Error.Validation("outbound_order.missing_ship_to", "shipTo wajib diisi.");

    public static readonly Error NoOrderLines =
        Error.Validation("outbound_order.no_order_lines", "order minimal punya satu line.");

    public static readonly Error MissingSku =
        Error.Validation("outbound_order.missing_sku", "sku wajib diisi.");

    public static readonly Error NonPositiveQuantity =
        Error.Validation("outbound_order.non_positive_quantity", "qty harus lebih dari nol.");

    public static readonly Error MissingUom =
        Error.Validation("outbound_order.missing_uom", "uom wajib diisi.");

    public static readonly Error InvalidWaveAssignment =
        Error.Conflict("outbound_order.invalid_wave_assignment", "hanya order New yang dapat dimasukkan ke wave.");

    public static readonly Error InvalidClose =
        Error.Conflict("outbound_order.invalid_close", "hanya order InProgress yang dapat ditutup.");
}
