using Wms.BuildingBlocks.Application.Security;

namespace Wms.Auth.Application.Security;

// What: Options (config policy) — parameter penerbitan token & lockout
// Why: lifetime token, issuer/audience, dan lock-threshold adalah POLICY (di-bind dari config host),
// bukan invariant domain — dipisah dari aggregate supaya kalibrasi tanpa sentuh business logic. Access
// token SHORT-LIVED (default 15m) ditebus refresh (default 7d) → window kompromi sempit (ADR-0016).
// Di-register sebagai instance singleton oleh host (plain record, bukan IOptions — selaras codebase).
public sealed class AuthTokenOptions
{
    // issuer (`iss`) — auth-svc; divalidasi offline di semua host (alg-pin, ADR-0016). Single source
    // WmsJwtDefaults supaya issuer (produsen) & host validator (konsumen) tak drift.
    public string Issuer { get; init; } = WmsJwtDefaults.Issuer;

    // audience (`aud`) — API plane; host menolak token aud lain (ADR-0016)
    public string Audience { get; init; } = WmsJwtDefaults.Audience;

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(7);

    // ambang failedLoginCount → auto-Lock (overview §E lockout policy)
    public int LockThreshold { get; init; } = 5;
}
