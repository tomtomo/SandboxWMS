using Wms.BuildingBlocks.Domain.Results;

namespace Wms.MasterData.Domain;

// What: katalog Error domain Product (Result pattern, ADR-0019)
// Why: invariant master (sku/name/uom wajib; shelfLifeDays positif bila diisi) = Validation (→400);
// transisi soft-delete ilegal = Conflict (→409).
public static class ProductErrors
{
    public static readonly Error NotFound =
        Error.NotFound("product.not_found", "Product tidak ditemukan.");

    public static readonly Error MissingSku =
        Error.Validation("product.missing_sku", "sku wajib diisi.");

    public static readonly Error MissingName =
        Error.Validation("product.missing_name", "name wajib diisi.");

    public static readonly Error MissingUom =
        Error.Validation("product.missing_uom", "uom wajib diisi.");

    public static readonly Error InvalidShelfLife =
        Error.Validation("product.invalid_shelf_life", "shelfLifeDays harus lebih dari nol bila diisi.");

    public static readonly Error AlreadyInactive =
        Error.Conflict("product.already_inactive", "product sudah non-aktif.");

    public static readonly Error AlreadyActive =
        Error.Conflict("product.already_active", "product sudah aktif.");
}
