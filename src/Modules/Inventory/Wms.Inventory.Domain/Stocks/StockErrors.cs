using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inventory.Domain;

// What: katalog Error domain Stock (Result pattern, ADR-0019)
// Why: kegagalan bisnis sebagai nilai ber-Code stabil — guard defensif walau data datang dari event
// yang sudah tervalidasi di produser. Transisi ilegal = Conflict (state sekarang bentrok dgn operasi
// yang diminta) → otomatis 409 di transport (ADR-0019); input kosong = Validation → 400.
public static class StockErrors
{
    public static readonly Error NotFound =
        Error.NotFound("stock.not_found", "Stock tidak ditemukan.");

    public static readonly Error MissingWarehouse =
        Error.Validation("stock.missing_warehouse", "warehouseId wajib diisi.");

    public static readonly Error MissingSku =
        Error.Validation("stock.missing_sku", "sku wajib diisi.");

    public static readonly Error MissingLocation =
        Error.Validation("stock.missing_location", "locationId wajib diisi.");

    public static readonly Error NonPositiveQuantity =
        Error.Validation("stock.non_positive_quantity", "quantity harus lebih dari nol.");

    public static readonly Error NegativeQuantity =
        Error.Validation("stock.negative_quantity", "quantity hasil koreksi tidak boleh negatif.");

    public static readonly Error InvalidPutaway =
        Error.Conflict("stock.invalid_putaway", "hanya stock OnHand yang dapat di-putaway.");

    public static readonly Error InvalidAllocation =
        Error.Conflict("stock.invalid_allocation", "hanya stock Available yang dapat dialokasi.");

    // What: split alokasi parsial dengan quantity tak valid (≤0 atau ≥ qty stock) — Validation → 400
    // Why: porsi PENUH (quantity == Quantity) pakai Allocate, bukan split; quantity ≤ 0 atau melebihi
    // saldo lot melanggar invariant pembagian balance (konservasi). Beda dari NonPositiveQuantity (factory).
    public static readonly Error InvalidSplitQuantity =
        Error.Validation("stock.invalid_split_quantity",
            "quantity split alokasi harus > 0 dan < quantity stock (porsi penuh pakai Allocate).");

    public static readonly Error InvalidPick =
        Error.Conflict("stock.invalid_pick", "hanya stock Allocated yang dapat dipick.");
}
