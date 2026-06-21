using Wms.BuildingBlocks.Domain.Results;

namespace Wms.MasterData.Domain;

// What: katalog Error domain Warehouse (Result pattern, ADR-0019)
// Why: kegagalan bisnis sebagai nilai ber-Code stabil — invariant master (name/address wajib) =
// Validation (otomatis 400 di transport); transisi soft-delete ilegal (double deactivate/activate) =
// Conflict (otomatis 409). Menambah nilai = keputusan sadar, bukan diam-diam.
public static class WarehouseErrors
{
    public static readonly Error NotFound =
        Error.NotFound("warehouse.not_found", "Warehouse tidak ditemukan.");

    public static readonly Error MissingName =
        Error.Validation("warehouse.missing_name", "name wajib diisi.");

    public static readonly Error MissingAddress =
        Error.Validation("warehouse.missing_address", "address wajib diisi.");

    public static readonly Error AlreadyInactive =
        Error.Conflict("warehouse.already_inactive", "warehouse sudah non-aktif.");

    public static readonly Error AlreadyActive =
        Error.Conflict("warehouse.already_active", "warehouse sudah aktif.");
}
