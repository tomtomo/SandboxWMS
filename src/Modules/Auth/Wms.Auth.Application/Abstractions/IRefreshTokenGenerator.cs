namespace Wms.Auth.Application.Abstractions;

// What: material refresh token baru — token MENTAH (ke client) + HASH (ke DB)
// Why: pemisahan eksplisit menegakkan invariant ADR-0016 — raw HANYA dikembalikan sekali ke client,
// DB menyimpan HANYA hash. Caller tak boleh keliru mempersist raw.
public sealed record RefreshTokenMaterial(string RawToken, string TokenHash);

// What: Port (Hexagonal; ADR-0016) — generator & hasher refresh token
// Why: pembuatan random 32-byte + SHA-256 = crypto BCL yang disembunyikan di balik port supaya slice
// testable (fake deterministik) & mekanisme swappable. SHA-256 (fast hash) SAH untuk token high-entropy
// random — beda dari password (KDF lambat Argon2id) karena refresh token tak bisa di-brute-force.
// How: Generate() = 32-byte RNG → raw base64url + hash; Hash(raw) = hash token yang disajikan untuk
// lookup by-hash (Refresh/Logout). Hash deterministik → presented-raw cocok dengan stored-hash.
public interface IRefreshTokenGenerator
{
    RefreshTokenMaterial Generate();

    string Hash(string rawToken);
}
