using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Domain;

// What: katalog Error domain Role (Result pattern, ADR-0019)
// Why: invariant code/name wajib = Validation (→400); transisi soft-delete ilegal = Conflict (→409).
public static class RoleErrors
{
    public static readonly Error NotFound =
        Error.NotFound("role.not_found", "Role tidak ditemukan.");

    public static readonly Error MissingCode =
        Error.Validation("role.missing_code", "code wajib diisi.");

    public static readonly Error MissingName =
        Error.Validation("role.missing_name", "name wajib diisi.");

    public static readonly Error AlreadyInactive =
        Error.Conflict("role.already_inactive", "role sudah non-aktif.");

    public static readonly Error AlreadyActive =
        Error.Conflict("role.already_active", "role sudah aktif.");
}
