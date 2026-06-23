using Wms.BuildingBlocks.Domain.Results;

namespace Wms.MasterData.Domain;

// What: katalog Error domain Location (Result pattern, ADR-0019)
// Why: invariant master (warehouseId/code wajib) = Validation (→400); transisi soft-delete ilegal =
// Conflict (→409).
public static class LocationErrors
{
    public static readonly Error NotFound =
        Error.NotFound("location.not_found", "Location tidak ditemukan.");

    public static readonly Error InvalidType =
        Error.Validation("location.invalid_type", "Tipe lokasi tidak dikenal.");

    public static readonly Error MissingWarehouse =
        Error.Validation("location.missing_warehouse", "warehouseId wajib diisi.");

    public static readonly Error MissingCode =
        Error.Validation("location.missing_code", "code wajib diisi.");

    public static readonly Error AlreadyInactive =
        Error.Conflict("location.already_inactive", "location sudah non-aktif.");

    public static readonly Error AlreadyActive =
        Error.Conflict("location.already_active", "location sudah aktif.");
}
