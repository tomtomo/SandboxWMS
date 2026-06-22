using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Application.Security;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.Auth.Infrastructure.Security;

// What: Adapter (Hexagonal) — IAccessTokenIssuer impl RS256 asymmetric (ADR-0016)
// Why: auth-svc menandatangani access JWT dengan PRIVATE key (dari ISecretProvider) — host lain verify
// OFFLINE dgn public key (anti shared-secret, anti alg-confusion bila dipasangkan alg-pin di validator).
// FAIL-SECURE: secret kosong → throw saat penerbitan pertama (tak pernah terbitkan token tak ber-sign).
// How: SigningCredentials(RsaSecurityKey, RsaSha256) di-cache thread-safe (key di-load sekali); claim
// sub/name/jti registered + role/permission/warehouse kustom (WmsClaims). Singleton (cache key hidup
// selama proses). throw di Infrastructure SAH (FF#7 hanya membatasi *.Domain).
public sealed class Rs256AccessTokenIssuer(ISecretProvider secretProvider, AuthTokenOptions options)
    : IAccessTokenIssuer
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SigningCredentials? _credentials;

    public async Task<IssuedAccessToken> IssueAsync(
        AccessTokenClaims claims, CancellationToken cancellationToken = default)
    {
        var credentials = await GetCredentialsAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(options.AccessTokenLifetime);

        var jwtClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, claims.UserId.ToString()),
            new(JwtRegisteredClaimNames.Name, claims.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        jwtClaims.AddRange(claims.RoleCodes.Select(code => new Claim(WmsClaims.Role, code)));
        jwtClaims.AddRange(claims.PermissionCodes.Select(code => new Claim(WmsClaims.Permission, code)));
        jwtClaims.AddRange(claims.WarehouseIds.Select(id => new Claim(WmsClaims.Warehouse, id.ToString())));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: jwtClaims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedAccessToken(value, expires);
    }

    // What: resolve & cache SigningCredentials (RS256) dari private key PEM — fail-secure bila kosong
    private async Task<SigningCredentials> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        if (_credentials is not null)
            return _credentials;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_credentials is not null)
                return _credentials;

            var pem = await secretProvider.GetSecretAsync(AuthSecretNames.JwtSigningKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(pem))
                throw new InvalidOperationException(
                    $"Secret '{AuthSecretNames.JwtSigningKey}' kosong — RS256 signing key wajib diset (fail-secure, ADR-0016).");

            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            // CacheSignatureProviders default true → RsaSecurityKey aman dipakai berulang
            _credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
            return _credentials;
        }
        finally
        {
            _gate.Release();
        }
    }
}
