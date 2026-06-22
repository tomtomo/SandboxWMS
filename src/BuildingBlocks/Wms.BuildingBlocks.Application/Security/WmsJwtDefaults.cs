namespace Wms.BuildingBlocks.Application.Security;

// What: kontrak iss/aud user-JWT plane (ADR-0016/0021) — single source
// Why: SEMUA host harus sepakat issuer & audience untuk validasi offline (ADR-0016 alg-pin + iss/aud).
// Disentralisasi supaya PRODUSEN (Auth issuer via AuthTokenOptions default) & KONSUMEN (host
// AddWmsJwtBearer) memakai nilai SAMA — drift iss/aud = token valid ditolak. "user JWT" (RS256 auth-svc)
// dibedakan tegas dari "s2s token" (platform-issued, ADR-0021) — ini bidang user.
public static class WmsJwtDefaults
{
    public const string Issuer = "wms-auth";

    public const string Audience = "wms-api";

    // skema bearer untuk AddAuthentication/AddJwtBearer di host
    public const string Scheme = "Bearer";
}
