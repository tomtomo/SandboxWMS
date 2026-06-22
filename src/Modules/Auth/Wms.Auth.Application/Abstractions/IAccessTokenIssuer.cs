namespace Wms.Auth.Application.Abstractions;

// What: data claim untuk access JWT (DTO) — input penerbitan token
public sealed record AccessTokenClaims(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> RoleCodes,
    IReadOnlyCollection<string> PermissionCodes,
    IReadOnlyCollection<Guid> WarehouseIds);

// What: access token yang sudah diterbitkan + waktu kedaluwarsanya
public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAt);

// What: Port (Hexagonal; ADR-0016) — penerbit access JWT RS256
// Why: penandatanganan JWT memakai library crypto + private key (ISecretProvider) = detail Infrastructure;
// Application hanya tahu kontrak ini (DIP). RS256 ASYMMETRIC — auth-svc menandatangani dgn private key,
// host lain verify OFFLINE dgn public key (ADR-0016/0021). Disembunyikan di balik port supaya slice
// Login/Refresh tak menyeret System.IdentityModel ke Application.
// How: IssueAsync memetakan AccessTokenClaims → JWT ber-sign (sub/name + role/permission/warehouse claim);
// impl me-resolve & cache private key dari ISecretProvider. Async karena fetch key bisa I/O (Key Vault).
public interface IAccessTokenIssuer
{
    Task<IssuedAccessToken> IssueAsync(AccessTokenClaims claims, CancellationToken cancellationToken = default);
}
