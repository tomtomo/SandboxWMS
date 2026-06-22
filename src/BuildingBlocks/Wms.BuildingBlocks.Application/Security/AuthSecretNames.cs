namespace Wms.BuildingBlocks.Application.Security;

// What: logical name secret material JWT (ADR-0016) — kontrak antara issuer & validator
// Why: satu sumbu konstanta supaya nama secret tak tersebar magic-string antara PRODUSEN (Auth issuer
// resolve private key) & DISTRIBUTOR/KONSUMEN (ISecretProvider adapter, host validation wiring). Private
// key = RAHASIA (signing, Auth-svc only); public key = NON-rahasia (didistribusi ke semua host untuk
// verify offline, ADR-0016/0021).
public static class AuthSecretNames
{
    // RS256 private key PEM (PKCS#8) — signing, hanya Auth-svc
    public const string JwtSigningKey = "auth-jwt-signing-key";

    // RS256 public key PEM (SPKI) — verify offline di SEMUA host (bukan rahasia)
    public const string JwtPublicKey = "auth-jwt-public-key";
}
