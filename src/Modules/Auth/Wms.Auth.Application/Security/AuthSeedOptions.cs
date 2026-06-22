namespace Wms.Auth.Application.Security;

// What: Options (config) — kredensial admin default yang di-seed (ADR-0012)
// Why: ADR-0012 menetapkan "1 user admin tersedia selama periode deferred". Kredensial di-bind dari config
// host agar TAK hard-coded di domain/logic. ⚠ DEV-ONLY default: password placeholder hanya untuk sandbox
// LOCAL (ADR-0012 "aman karena sandbox non-produksi") — WAJIB override `Auth:Seed:AdminPassword` di env
// non-local. Bukan secret produksi.
public sealed class AuthSeedOptions
{
    public string AdminUsername { get; init; } = "admin";

    public string AdminEmail { get; init; } = "admin@wms.local";

    // ⚠ DEV-ONLY placeholder — override via config di luar Local (credential hygiene)
    public string AdminPassword { get; init; } = "ChangeMe123!";
}
