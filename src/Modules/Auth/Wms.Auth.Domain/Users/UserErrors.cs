using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Domain;

// What: katalog Error domain User (Result pattern, ADR-0019)
// Why: invariant create (username/email/passwordHash wajib) = Validation (→400); transisi status ilegal
// = Conflict (→409). InvalidCredentials = Unauthorized (→401) dipakai jalur Login (handler) untuk
// kegagalan kredensial SERAGAM (anti user-enumeration, ADR-0016) — tak membedakan user-tak-ada vs
// password-salah vs akun-terkunci.
public static class UserErrors
{
    public static readonly Error NotFound =
        Error.NotFound("user.not_found", "User tidak ditemukan.");

    public static readonly Error MissingUsername =
        Error.Validation("user.missing_username", "username wajib diisi.");

    public static readonly Error MissingEmail =
        Error.Validation("user.missing_email", "email wajib diisi.");

    public static readonly Error MissingPasswordHash =
        Error.Validation("user.missing_password_hash", "passwordHash wajib diisi.");

    public static readonly Error NotActive =
        Error.Conflict("user.not_active", "user tidak dalam status Active.");

    public static readonly Error AlreadyDisabled =
        Error.Conflict("user.already_disabled", "user sudah disabled.");

    public static readonly Error NotLocked =
        Error.Conflict("user.not_locked", "user tidak dalam status Locked.");

    // What: kegagalan kredensial SERAGAM (Unauthorized) — anti user-enumeration (ADR-0016)
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "username atau password salah.");
}
