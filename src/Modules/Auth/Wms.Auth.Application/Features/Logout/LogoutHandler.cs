using MediatR;
using Wms.Auth.Application.Abstractions;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Application.Features.Logout;

// What: CQRS — Command Handler (MediatR) — revoke refresh token (ADR-0016)
// Why: logout = cabut sesi refresh. IDEMPOTEN: token tak dikenal atau sudah tercabut → tetap Success
// (tak bocorkan apakah token valid; Revoke idempotent menjaga timestamp pencabutan pertama).
// How: hash token disajikan → lookup → Revoke(now) bila ada → SaveChanges. Selalu Result.Success.
public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator generator,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var presentedHash = generator.Hash(command.RefreshToken);
        var token = await refreshTokens.GetByHashAsync(presentedHash, cancellationToken);

        if (token is not null)
        {
            token.Revoke(DateTimeOffset.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
