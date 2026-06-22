using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Domain;

// What: katalog Error domain RefreshToken (Result pattern, ADR-0019)
// Why: invariant issue (tokenHash wajib, expiry > issued) = Validation (→400); rotate token tak-aktif
// = Conflict (→409). NotActive juga dipakai jalur refresh untuk menandai token kedaluwarsa/tercabut.
public static class RefreshTokenErrors
{
    public static readonly Error MissingTokenHash =
        Error.Validation("refresh_token.missing_hash", "tokenHash wajib diisi.");

    public static readonly Error InvalidExpiry =
        Error.Validation("refresh_token.invalid_expiry", "expiresAt harus setelah issuedAt.");

    public static readonly Error NotActive =
        Error.Conflict("refresh_token.not_active", "refresh token sudah tercabut atau kedaluwarsa.");
}
