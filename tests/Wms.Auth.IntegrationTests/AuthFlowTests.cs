using System.IdentityModel.Tokens.Jwt;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.Auth.Application.DependencyInjection;
using Wms.Auth.Application.Features.Login;
using Wms.Auth.Application.Features.Logout;
using Wms.Auth.Application.Features.Refresh;
using Wms.Auth.Application.Security;
using Wms.Auth.Infrastructure.DependencyInjection;
using Wms.Auth.Infrastructure.Persistence;
using Wms.Auth.Infrastructure.Security;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Auth.IntegrationTests;

// What: integration test slice Login/Refresh/Logout atas Postgres riil (DoD Phase 04b) — ADR-0016
// Why: membuktikan DoD inti via slice (ISender, pipeline penuh) atas authority Auth nyata: login terbitkan
// access JWT RS256 (claim sub/role/permission) + refresh; refresh ROTASI (token lama tercabut); token
// tercabut disajikan ulang → REUSE-DETECTION CASCADE seluruh rantai; kredensial salah → InvalidCredentials
// seragam + lockout. Argon2id verify nyata, RS256 sign nyata (key ephemeral LocalSecretProvider).
[Collection(PostgresCollection.Name)]
public sealed class AuthFlowTests(PostgresFixture fixture)
{
    private const string AdminPassword = "ChangeMe123!";

    [Fact]
    public async Task Login_issues_tokens_then_refresh_rotates_and_reuse_cascades_revoke()
    {
        await using var auth = await BuildSeededAuthAsync();
        var adminId = await GetAdminIdAsync(auth);

        // (1) login → access JWT RS256 (sub=adminId + role ADMIN + permission catalog) + refresh
        var tokens1 = await SendAsync(auth, new LoginCommand("admin", AdminPassword));
        Assert.True(tokens1.IsSuccess);

        var accessJwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens1.Value.AccessToken);
        Assert.Equal(adminId.ToString(), accessJwt.Subject);
        Assert.Contains(accessJwt.Claims, c => c is { Type: "role", Value: "ADMIN" });
        Assert.Contains(accessJwt.Claims, c => c is { Type: "permission", Value: "Inbound.PostGR" });
        Assert.False(string.IsNullOrWhiteSpace(tokens1.Value.RefreshToken));

        // (2) refresh → ROTASI: token baru, beda dari yang lama
        var tokens2 = await SendAsync(auth, new RefreshCommand(tokens1.Value.RefreshToken));
        Assert.True(tokens2.IsSuccess);
        Assert.NotEqual(tokens1.Value.RefreshToken, tokens2.Value.RefreshToken);

        // (3) REUSE: token lama (sudah tercabut saat rotasi) disajikan ulang → ditolak (replay defense)
        var reuse = await SendAsync(auth, new RefreshCommand(tokens1.Value.RefreshToken));
        Assert.True(reuse.IsFailure);
        Assert.Equal("refresh_token.not_active", reuse.Error.Code);

        // (4) CASCADE: deteksi reuse mencabut SELURUH rantai → token penerus (tokens2) ikut tercabut
        var afterCascade = await SendAsync(auth, new RefreshCommand(tokens2.Value.RefreshToken));
        Assert.True(afterCascade.IsFailure);
        Assert.Equal("refresh_token.not_active", afterCascade.Error.Code);
    }

    [Fact]
    public async Task Concurrent_refresh_does_not_fork_rotation_chain()
    {
        await using var auth = await BuildSeededAuthAsync();

        var login = await SendAsync(auth, new LoginCommand("admin", AdminPassword));
        Assert.True(login.IsSuccess);
        var refreshToken = login.Value.RefreshToken;

        // dua refresh PARALEL atas token yang SAMA (request-concurrency, DbContext berbeda per scope).
        // Tanpa optimistic concurrency token (ADR-0031): keduanya baca token aktif → dua successor aktif
        // (rotation-fork → replay-detection defeat). Dengan xmin: hanya satu commit; yang lain Conflict/rollback.
        var results = await Task.WhenAll(
            SendAsync(auth, new RefreshCommand(refreshToken)),
            SendAsync(auth, new RefreshCommand(refreshToken)));

        // tepat satu refresh sukses (pemenang race) — tak pernah dua (loser → concurrency.conflict atau,
        // bila terbaca setelah commit, reuse-detection not_active). Liveness: satu selalu menang.
        Assert.Equal(1, results.Count(result => result.IsSuccess));

        // INVARIAN no-fork (ADR-0031): tak pernah ada >1 refresh token aktif sekaligus — inti yang xmin tutup.
        var now = DateTimeOffset.UtcNow;
        using var scope = auth.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var activeTokens = (await db.RefreshTokens.ToListAsync()).Count(token => token.IsActive(now));
        Assert.True(activeTokens <= 1, $"rotation-fork: {activeTokens} refresh token aktif (invarian ≤ 1).");
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_idempotently()
    {
        await using var auth = await BuildSeededAuthAsync();

        var login = await SendAsync(auth, new LoginCommand("admin", AdminPassword));
        Assert.True(login.IsSuccess);

        // logout mencabut refresh → refresh berikutnya gagal; logout ulang tetap sukses (idempoten)
        await SendVoidAsync(auth, new LogoutCommand(login.Value.RefreshToken));
        await SendVoidAsync(auth, new LogoutCommand(login.Value.RefreshToken));

        var afterLogout = await SendAsync(auth, new RefreshCommand(login.Value.RefreshToken));
        Assert.True(afterLogout.IsFailure);
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_account()
    {
        await using var auth = await BuildSeededAuthAsync();

        // 5 percobaan salah (threshold default) → akun Locked. Membuktikan failed-login increment
        // PERSIST OUT-OF-BAND: tanpa itu tiap increment ter-rollback (command Failure) → tak pernah lock.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await SendAsync(auth, new LoginCommand("admin", "wrong-password"));
            Assert.True(failed.IsFailure);
        }

        // password BENAR pun ditolak — akun terkunci (lockout aktif karena counter persist)
        var locked = await SendAsync(auth, new LoginCommand("admin", AdminPassword));
        Assert.True(locked.IsFailure);
        Assert.Equal("auth.invalid_credentials", locked.Error.Code);
    }

    [Theory]
    [InlineData("admin", "wrong-password")]
    [InlineData("ghost", "whatever")]
    public async Task Login_fails_uniformly_for_bad_credentials(string username, string password)
    {
        await using var auth = await BuildSeededAuthAsync();

        var result = await SendAsync(auth, new LoginCommand(username, password));

        // SERAGAM: user-tak-ada & password-salah → InvalidCredentials yang SAMA (anti user-enumeration)
        Assert.True(result.IsFailure);
        Assert.Equal("auth.invalid_credentials", result.Error.Code);
    }

    private async Task<ServiceProvider> BuildSeededAuthAsync()
    {
        var connection = await fixture.CreateDatabaseAsync();
        var provider = new ServiceCollection()
            .AddLogging()
            .AddAuthApplication()
            .AddAuthInfrastructure(connection)
            .AddLocalPasswordHasher()   // Argon2id verify nyata
            .AddLocalSecretProvider()   // RS256 key ephemeral (sign access JWT)
            .AddLocalCaching()
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AuthSeeder>().SeedAsync();
        return provider;
    }

    private static async Task<Guid> GetAdminIdAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var admin = await db.Users.SingleAsync(user => user.Username == "admin");
        return admin.Id.Value;
    }

    private static async Task<Result<AuthTokens>> SendAsync(
        IServiceProvider provider, IRequest<Result<AuthTokens>> command)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    private static async Task SendVoidAsync(IServiceProvider provider, IRequest<Result> command)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }
}
