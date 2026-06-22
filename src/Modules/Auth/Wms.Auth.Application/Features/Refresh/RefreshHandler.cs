using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Application.Security;
using Wms.Auth.Domain;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Application.Features.Refresh;

// What: CQRS — Command Handler (MediatR) — rotate refresh token + reuse-detection (ADR-0016)
// Why: jantung rotation chain + replay defense. Token disajikan → hash → lookup. AKTIF → rotate (terbit
// pengganti, tandai ReplacedByTokenId + revoke lama, atomic). TAK-AKTIF & sudah tercabut = REUSE
// terdeteksi → CASCADE revoke seluruh rantai (walk ReplacedByTokenId) → tolak. User harus tetap Active
// (IsActive filter mint, ADR-0012). Hash-only: token mentah tak pernah dibanding plain.
// How: lookup by-hash → IsActive(now)? rotate+mint : (revoked? cascade) tolak. TransactionBehavior
// menjamin rotate+revoke+new-token ATOMIK (revocation cascade tak boleh setengah jadi, ADR-0016).
public sealed class RefreshHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IRefreshTokenGenerator generator,
    TokenMinter tokenMinter,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopeFactory)
    : IRequestHandler<RefreshCommand, Result<AuthTokens>>
{
    public async Task<Result<AuthTokens>> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var presentedHash = generator.Hash(command.RefreshToken);
        var token = await refreshTokens.GetByHashAsync(presentedHash, cancellationToken);

        if (token is null)
            return Result.Failure<AuthTokens>(RefreshTokenErrors.NotActive);

        if (!token.IsActive(now))
        {
            // REUSE detection (ADR-0016): token TERCABUT disajikan ulang → cascade revoke seluruh rantai
            // OUT-OF-BAND: revocation HARUS survive rollback (command Failure → TransactionBehavior rollback).
            if (token.RevokedAt is not null)
                await CascadeRevokeOutOfBandAsync(token.Id, now, cancellationToken);

            return Result.Failure<AuthTokens>(RefreshTokenErrors.NotActive);
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.CanAuthenticate)
            return Result.Failure<AuthTokens>(UserErrors.InvalidCredentials);

        var minted = await tokenMinter.MintAsync(user, now, cancellationToken);

        var rotate = token.Rotate(minted.RefreshToken.Id, now);
        if (rotate.IsFailure)
            return Result.Failure<AuthTokens>(rotate.Error);

        await refreshTokens.AddAsync(minted.RefreshToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(minted.Tokens);
    }

    // What: cascade revoke OUT-OF-BAND (ADR-0016) — walk rotation chain via ReplacedByTokenId di scope baru.
    // Why: reuse token tercabut = sinyal kompromi → seluruh rantai sesi (termasuk token aktif penerus yang
    // dipegang penyerang/korban) dicabut. Command Refresh mengembalikan Result.Failure (reuse) → Transaction
    // Behavior rollback transaksi command; revocation = security side-effect yang HARUS persist meski refresh
    // ditolak → ditulis di DbContext SEGAR (scope independen, pola AuditLogBehavior ADR-0022) yang commit
    // lepas dari rollback command. Revoke idempoten → aman walau ada simpul sudah tercabut.
    private async Task CascadeRevokeOutOfBandAsync(
        RefreshTokenId startId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var scopedTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var current = await scopedTokens.GetByIdAsync(startId, cancellationToken);
        while (current is not null)
        {
            current.Revoke(now);
            current = current.ReplacedByTokenId is { } next
                ? await scopedTokens.GetByIdAsync(next, cancellationToken)
                : null;
        }

        await scopedUnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
