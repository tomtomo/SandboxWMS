using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.BuildingBlocks.Web.Security;

// What: shared offline JWT validation helper (ADR-0016/0021) — dipakai SEMUA host
// Why: validasi token user di-lakukan OFFLINE di tiap host (public key, tanpa RPC ke Auth per-request,
// hot-path bersih ADR-0012). SATU helper supaya kebijakan validasi tak tersebar & konsisten: ALG-PINNING
// `ValidAlgorithms=[RS256]` (tolak `alg:none`/HS256 → anti alg-confusion), validasi iss/aud/exp/nbf,
// RequireSignedTokens, FAIL-SECURE (key kosong → throw saat startup, tak diam-diam terima token apa pun).
// → Canon: OWASP ASVS JWT/JWS validation. Builder MURNI (BuildValidationParameters) testable tanpa host
// (dipakai negative-security behavioral test, registry ADR-0003).
// How: AddWmsJwtBearer wire AddJwtBearer + resolve public key LAZY via ISecretProvider (saat options
// pertama dipakai, setelah DI terbangun) → BuildValidationParameters alg-pinned.
public static class WmsJwtBearer
{
    // What: builder MURNI TokenValidationParameters alg-pinned RS256 (fail-secure bila key kosong)
    public static TokenValidationParameters BuildValidationParameters(
        string publicKeyPem, string issuer, string audience)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPem))
            throw new InvalidOperationException(
                "Public key JWT kosong — fail-secure (ADR-0016): host menolak validasi tanpa key.");

        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsa),
            // ALG-PINNING (anti alg-confusion): HANYA RS256 — tolak HS256 & `alg:none`
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    // What: wire JWT bearer offline-validation ke host (issuer/aud default WmsJwtDefaults)
    public static IServiceCollection AddWmsJwtBearer(
        this IServiceCollection services, string? issuer = null, string? audience = null)
    {
        services.AddAuthentication(WmsJwtDefaults.Scheme).AddJwtBearer(WmsJwtDefaults.Scheme);
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>>(sp =>
            new ConfigureWmsJwtBearerOptions(
                sp.GetRequiredService<ISecretProvider>(),
                issuer ?? WmsJwtDefaults.Issuer,
                audience ?? WmsJwtDefaults.Audience));
        return services;
    }
}

// What: konfigurasi JwtBearerOptions yang me-resolve public key LAZY dari ISecretProvider (ADR-0016)
// Why: public key di-resolve via port (env/ephemeral Local; Key Vault/Secret Manager cloud) SETELAH DI
// terbangun — tak bisa di-resolve di composition root sync tanpa memecah singleton ephemeral. GetSecret
// Local synchronous-completed → GetAwaiter().GetResult() aman (tak deadlock).
internal sealed class ConfigureWmsJwtBearerOptions(ISecretProvider secretProvider, string issuer, string audience)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != WmsJwtDefaults.Scheme)
            return;

        var publicKey = secretProvider.GetSecretAsync(AuthSecretNames.JwtPublicKey).GetAwaiter().GetResult();
        options.TokenValidationParameters =
            WmsJwtBearer.BuildValidationParameters(publicKey ?? string.Empty, issuer, audience);
    }

    public void Configure(JwtBearerOptions options) => Configure(WmsJwtDefaults.Scheme, options);
}
