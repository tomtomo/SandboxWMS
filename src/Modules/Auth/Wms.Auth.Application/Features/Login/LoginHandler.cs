using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Application.Security;
using Wms.Auth.Domain;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Application.Features.Login;

// What: CQRS — Command Handler (MediatR) — authenticate + mint token (ADR-0016)
// Why: satu-satunya jalur verify kredensial. TIMING-SAFE (anti user-enumeration, ADR-0016): user tak
// dikenal → tetap jalankan dummy-verify (hasher.Sentinel) supaya waktu respons setara jalur user-ada;
// semua kegagalan kredensial → Error SERAGAM InvalidCredentials (tak bedakan user-tak-ada/password-salah/
// akun-terkunci). REHASH-ON-UPGRADE: hash usang ditebus transparan saat login sukses. Lockout via
// RecordFailedLogin (policy threshold). MINT via TokenMinter (IsActive-filtered claim, ADR-0012).
// How: lookup → verify constant-time → (gagal) RecordFailedLogin+save → (sukses & Active) rehash?+reset+
// mint+persist refresh. TransactionBehavior membungkus atomik; IUnitOfWork commit di pipeline.
public sealed class LoginHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    TokenMinter tokenMinter,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopeFactory,
    AuthTokenOptions options)
    : IRequestHandler<LoginCommand, Result<AuthTokens>>
{
    public async Task<Result<AuthTokens>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var user = await users.GetByUsernameAsync(command.Username, cancellationToken);

        if (user is null)
        {
            // timing-safe: dummy verify vs sentinel agar waktu respons tak bocorkan eksistensi username
            passwordHasher.Verify(command.Password, passwordHasher.Sentinel);
            return Result.Failure<AuthTokens>(UserErrors.InvalidCredentials);
        }

        var verification = passwordHasher.Verify(command.Password, user.PasswordHash);
        if (verification == PasswordVerificationResult.Failed)
        {
            // OUT-OF-BAND: lockout counter HARUS survive rollback (command Failure → TransactionBehavior
            // rollback). Tanpa ini increment hilang → lockout tak pernah aktif (pola AuditLogBehavior ADR-0022).
            await RecordFailedLoginOutOfBandAsync(user.Id, cancellationToken);
            return Result.Failure<AuthTokens>(UserErrors.InvalidCredentials);
        }

        // password benar TAPI akun tak layak (Locked/Disabled) → SERAGAM InvalidCredentials (tak bocorkan)
        if (!user.CanAuthenticate)
            return Result.Failure<AuthTokens>(UserErrors.InvalidCredentials);

        // rehash-on-upgrade (ADR-0016): parameter KDF usang → stempel ulang hash transparan
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.ChangePasswordHash(passwordHasher.Hash(command.Password));

        user.RecordSuccessfulLogin();

        var minted = await tokenMinter.MintAsync(user, now, cancellationToken);
        await refreshTokens.AddAsync(minted.RefreshToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(minted.Tokens);
    }

    // What: catat failed-login OUT-OF-BAND (scope/koneksi baru; pola ADR-0022) — survive rollback command.
    // Why: command Login mengembalikan Result.Failure pada kredensial salah → TransactionBehavior rollback
    // transaksi command. Lockout counter adalah security side-effect yang HARUS persist meski login ditolak
    // → ditulis di DbContext SEGAR (scope independen) yang commit lepas dari transaksi command.
    private async Task RecordFailedLoginOutOfBandAsync(UserId userId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var scopedUsers = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var user = await scopedUsers.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return;

        user.RecordFailedLogin(options.LockThreshold);
        await scopedUnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
