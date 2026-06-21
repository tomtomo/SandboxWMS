using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inventory.Domain;

// What: katalog Error domain Stock (Result pattern, ADR-0019)
// Why: kegagalan bisnis sebagai nilai ber-Code stabil — guard defensif walau data
// datang dari event yang sudah tervalidasi di produser.
public static class StockErrors
{
    public static readonly Error MissingWarehouse =
        Error.Validation("stock.missing_warehouse", "warehouseId wajib diisi.");

    public static readonly Error MissingSku =
        Error.Validation("stock.missing_sku", "sku wajib diisi.");

    public static readonly Error NonPositiveQuantity =
        Error.Validation("stock.non_positive_quantity", "quantity harus lebih dari nol.");
}
