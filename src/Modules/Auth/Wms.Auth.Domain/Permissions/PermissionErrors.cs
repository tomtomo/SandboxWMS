using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Domain;

// What: katalog Error domain Permission (Result pattern, ADR-0019)
// Why: code = natural key reference entity → wajib non-empty (Validation →400).
public static class PermissionErrors
{
    public static readonly Error NotFound =
        Error.NotFound("permission.not_found", "Permission tidak ditemukan.");

    public static readonly Error MissingCode =
        Error.Validation("permission.missing_code", "code wajib diisi.");
}
