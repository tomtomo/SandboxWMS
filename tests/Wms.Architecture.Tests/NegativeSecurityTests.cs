using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Wms.BuildingBlocks.Web.Security;

namespace Wms.Architecture.Tests;

// What: behavioral fitness function `negative-security` (ADR-0016) — registry ADR-0003
// Why: BUKAN NetArchTest — meng-EKSEKUSI helper validasi JWT bersama (WmsJwtBearer) terhadap token jahat.
// Menjaga insight load-bearing "offline validation alg-pinned": token `alg:none` (unsigned), HS256
// (alg-confusion — penyerang menukar RS256→HS256 lalu sign pakai public key sebagai secret), dan wrong-aud
// HARUS DITOLAK; hanya RS256 ber-sign benar dgn iss/aud benar yang diterima. Plus fail-secure: key kosong
// → helper throw (host tak boleh start menerima token apa pun). → Canon: OWASP ASVS JWT/JWS validation.
public class NegativeSecurityTests
{
    private const string Issuer = "wms-auth";
    private const string Audience = "wms-api";

    private static readonly RSA SigningRsa = RSA.Create(2048);
    private static readonly string PublicKeyPem = SigningRsa.ExportSubjectPublicKeyInfoPem();

    private static TokenValidationParameters Parameters() =>
        WmsJwtBearer.BuildValidationParameters(PublicKeyPem, Issuer, Audience);

    [Fact]
    public void Valid_rs256_token_with_correct_iss_aud_is_accepted()
    {
        var token = Rs256Token(Issuer, Audience);

        Assert.True(Validates(token, Parameters()));
    }

    [Fact]
    public void Unsigned_alg_none_token_is_rejected()
    {
        // token tanpa SigningCredentials → header `alg:none`, signature kosong
        var unsigned = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(Issuer, Audience, Claims(), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1)));

        Assert.False(Validates(unsigned, Parameters()));
    }

    [Fact]
    public void Hs256_token_is_rejected_by_algorithm_pinning()
    {
        // alg-confusion: sign HS256 pakai public-key-bytes sebagai secret simetris
        var symmetricKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PublicKeyPem));
        var hs256 = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Issuer, Audience, Claims(), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1),
            new SigningCredentials(symmetricKey, SecurityAlgorithms.HmacSha256)));

        Assert.False(Validates(hs256, Parameters()));
    }

    [Fact]
    public void Wrong_audience_token_is_rejected()
    {
        var token = Rs256Token(Issuer, audience: "evil-aud");

        Assert.False(Validates(token, Parameters()));
    }

    [Fact]
    public void Empty_public_key_fails_secure()
    {
        // fail-secure: helper menolak membangun parameter tanpa key (host tak start)
        Assert.Throws<InvalidOperationException>(() =>
            WmsJwtBearer.BuildValidationParameters(string.Empty, Issuer, Audience));
    }

    private static string Rs256Token(string issuer, string audience)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(SigningRsa), SecurityAlgorithms.RsaSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer, audience, Claims(), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), credentials));
    }

    private static IEnumerable<Claim> Claims() =>
        [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())];

    private static bool Validates(string token, TokenValidationParameters parameters)
    {
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
