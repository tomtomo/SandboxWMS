using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inbound.Domain;

// What: katalog Error domain GoodsReceipt (Result pattern, ADR-0019)
// Why: kegagalan bisnis sebagai NILAI ber-Code stabil (bukan exception) — caller
// dipaksa handle eksplisit, dan Code dipakai untuk mapping transport (ProblemDetails).
public static class GoodsReceiptErrors
{
    public static readonly Error MissingWarehouse =
        Error.Validation("goods_receipt.missing_warehouse", "warehouseId wajib diisi.");

    public static readonly Error NoLines =
        Error.Validation("goods_receipt.no_lines", "GoodsReceipt minimal punya satu line.");

    public static readonly Error MissingSku =
        Error.Validation("goods_receipt.missing_sku", "sku line wajib diisi.");

    public static readonly Error NonPositiveQuantity =
        Error.Validation("goods_receipt.non_positive_quantity", "quantity line harus lebih dari nol.");

    public static readonly Error AlreadyConfirmed =
        Error.Conflict("goods_receipt.already_confirmed", "GoodsReceipt sudah Confirmed.");

    public static readonly Error NotFound =
        Error.NotFound("goods_receipt.not_found", "GoodsReceipt tidak ditemukan.");
}
