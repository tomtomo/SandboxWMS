using Wms.Auth.Application.Abstractions;
using Wms.Auth.Domain;

namespace Wms.Auth.Application.Security;

// What: hasil mint — token untuk client + aggregate RefreshToken untuk dipersist handler
public sealed record MintedTokens(AuthTokens Tokens, RefreshToken RefreshToken);

// What: Application Service (DDD) — merakit claim & menerbitkan pasangan token (ADR-0016/0012)
// Why: logika "mint token untuk user" IDENTIK di Login & Refresh — disentralisasi di sini supaya tak
// terduplikasi & aturan IsActive-filter (ADR-0012) ditegakkan SATU tempat: HANYA permission dari role
// AKTIF (GetActiveByIdsAsync) yang masuk claim self-contained. Bukan port — orkestrasi murni atas port
// (issuer + generator + role repo) → tetap testable, tak menyeret crypto/EF ke Application.
// How: gather active roles → role/permission code → IssueAsync (access JWT RS256) → Generate (refresh
// raw+hash) → RefreshToken.Issue (aggregate, hash-only). Handler yang AddAsync + SaveChanges (tx-control).
public sealed class TokenMinter(
    IAccessTokenIssuer issuer,
    IRefreshTokenGenerator generator,
    IRoleRepository roles,
    AuthTokenOptions options)
{
    public async Task<MintedTokens> MintAsync(User user, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // IsActive filter (ADR-0012): permission dari role non-aktif TAK boleh bocor ke JWT
        var activeRoles = await roles.GetActiveByIdsAsync(user.RoleIds, cancellationToken);
        var roleCodes = activeRoles.Select(role => role.Code).ToArray();
        var permissionCodes = activeRoles
            .SelectMany(role => role.PermissionCodes)
            .Distinct()
            .ToArray();

        var access = await issuer.IssueAsync(
            new AccessTokenClaims(
                user.Id.Value, user.Username, roleCodes, permissionCodes, user.AssignedWarehouseIds.ToArray()),
            cancellationToken);

        var material = generator.Generate();
        var refreshExpiry = now.Add(options.RefreshTokenLifetime);
        // hash-only (ADR-0016): aggregate menyimpan material.TokenHash; raw hanya keluar via AuthTokens
        var refreshToken = RefreshToken
            .Issue(RefreshTokenId.New(), user.Id, material.TokenHash, now, refreshExpiry)
            .Value;

        var tokens = new AuthTokens(access.Value, access.ExpiresAt, material.RawToken, refreshExpiry);
        return new MintedTokens(tokens, refreshToken);
    }
}
