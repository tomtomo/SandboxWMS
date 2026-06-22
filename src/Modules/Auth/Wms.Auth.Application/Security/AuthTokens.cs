namespace Wms.Auth.Application.Security;

// What: hasil penerbitan token (DTO) — pasangan access + refresh dikembalikan Login/Refresh
// Why: RefreshToken di sini = token MENTAH (ADR-0016) — satu-satunya titik di mana raw keluar ke client;
// server hanya menyimpan hash-nya. Expiry di-expose agar client tahu kapan refresh/re-login.
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
