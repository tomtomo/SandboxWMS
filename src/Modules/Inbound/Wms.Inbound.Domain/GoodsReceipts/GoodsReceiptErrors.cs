using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inbound.Domain;

// What: katalog Error domain GoodsReceipt (Result pattern, ADR-0019)
// Why: kegagalan bisnis sebagai NILAI ber-Code stabil (bukan exception) — caller dipaksa
// handle eksplisit, dan Code dipakai untuk mapping transport (ProblemDetails / gRPC status).
public static class GoodsReceiptErrors
{
    // --- factory / Create invariants ---
    public static readonly Error MissingWarehouse =
        Error.Validation("goods_receipt.missing_warehouse", "warehouseId wajib diisi.");

    public static readonly Error NoExpectedLines =
        Error.Validation("goods_receipt.no_expected_lines", "GoodsReceipt minimal punya satu expected line.");

    public static readonly Error MissingSku =
        Error.Validation("goods_receipt.missing_sku", "sku line wajib diisi.");

    public static readonly Error NonPositiveExpectedQuantity =
        Error.Validation("goods_receipt.non_positive_expected_quantity", "expectedQty harus lebih dari nol.");

    public static readonly Error MissingUom =
        Error.Validation("goods_receipt.missing_uom", "uom expected line wajib diisi (snapshot master, ADR-0014).");

    // --- scan / state-transition guards ---
    public static readonly Error NonPositiveScanQuantity =
        Error.Validation("goods_receipt.non_positive_scan_quantity", "actualQty scan harus lebih dari nol.");

    public static readonly Error NotInProgress =
        Error.Conflict("goods_receipt.not_in_progress", "operasi hanya legal saat GoodsReceipt InProgress.");

    public static readonly Error NotPending =
        Error.Conflict("goods_receipt.not_pending", "operasi hanya legal saat GoodsReceipt Pending.");

    public static readonly Error DiscrepancyNotFound =
        Error.NotFound("goods_receipt.discrepancy_not_found", "discrepancy (sku, type) tidak ditemukan.");

    // What: penjaga invariant two-axis (ADR-0013) — tiap discrepancy wajib ter-resolve sebelum Confirm
    public static readonly Error UnresolvedDiscrepancy =
        Error.Conflict("goods_receipt.unresolved_discrepancy", "semua discrepancy harus di-resolve sebelum Confirm.");

    public static readonly Error NotFound =
        Error.NotFound("goods_receipt.not_found", "GoodsReceipt tidak ditemukan.");
}
