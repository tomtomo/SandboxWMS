using System.Security.Cryptography;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.Platform.Local.Security;

// What: Adapter Local untuk port ISecretProvider (ADR-0002/0016)
// Why: di Local tak ada Key Vault/Secret Manager → resolve secret dari ENV VAR (konvensi `Secrets__{name}`,
// di-inject AppHost via WithEnvironment untuk distribusi keypair RS256 lintas-host), dengan FALLBACK
// EPHEMERAL: bila key JWT tak diset, generate dev RSA keypair sekali per-proses. Ephemeral menjaga
// konsistensi single-process (issuer sign + validator verify pakai instance SAMA → cocok) untuk
// integration test & single-host TANPA credential di source (credential hygiene). Cloud (Key Vault/
// Secret Manager) menggantikan adapter ini tanpa sentuh core (Hexagonal, FF#1).
// How: GetSecretAsync → env `Secrets__{name}` dulu; else ephemeral PEM untuk signing/public key JWT; else
// null (fail-secure di issuer). Keypair di-generate lazy thread-safe (Lazy), private PKCS#8 + public SPKI.
public sealed class LocalSecretProvider : ISecretProvider
{
    // What: dev RSA keypair ephemeral (per-proses) — generated saat key JWT diminta tanpa env
    private readonly Lazy<(string PrivatePem, string PublicPem)> _ephemeralJwtKeys = new(GenerateRsaKeyPair);

    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        // 1) env var (AppHost WithEnvironment "Secrets__{name}" → distribusi keypair shared lintas-host)
        var fromEnvironment = Environment.GetEnvironmentVariable($"Secrets__{name}");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return Task.FromResult<string?>(fromEnvironment);

        // 2) fallback ephemeral (single-process consistency: tests / single host) — nol credential di source
        var fallback = name switch
        {
            AuthSecretNames.JwtSigningKey => _ephemeralJwtKeys.Value.PrivatePem,
            AuthSecretNames.JwtPublicKey => _ephemeralJwtKeys.Value.PublicPem,
            _ => null,
        };
        return Task.FromResult(fallback);
    }

    private static (string PrivatePem, string PublicPem) GenerateRsaKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
    }
}
