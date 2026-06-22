using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Wms.Auth.Application.Abstractions;

namespace Wms.Auth.Infrastructure.Security;

// What: Adapter (Hexagonal) — IRefreshTokenGenerator impl (ADR-0016)
// Why: refresh token = 32-byte cryptographically-random (RandomNumberGenerator) → entropy tinggi tak bisa
// di-brute-force, jadi HASH cepat (SHA-256) SAH untuk penyimpanan (beda dari password yang butuh KDF
// lambat Argon2id). Raw base64url AMAN untuk transport URL/header. Hash deterministik → token disajikan
// di-hash lalu lookup by-hash. Singleton (stateless).
// How: Generate = 32-byte RNG → raw base64url + SHA-256 hex hash; Hash = SHA-256 hex dari raw (lookup).
public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenBytes = 32;

    public RefreshTokenMaterial Generate()
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(TokenBytes));
        return new RefreshTokenMaterial(raw, Hash(raw));
    }

    public string Hash(string rawToken)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(digest);
    }
}
