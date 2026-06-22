using System.Net.Http.Headers;
using System.Net.Http.Json;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.Auth.Grpc;
using Wms.Auth.Infrastructure.Persistence;
using Wms.Auth.Infrastructure.Security;
using Wms.TestSupport;

namespace Wms.Auth.IntegrationTests;

// What: integration test host Auth — gRPC read-API (cache-aside) + HTTP identity flow (DoD Phase 04b)
// Why: membuktikan (a) read-API gRPC GetUser via transport REAL (server in-proc) merakit role/permission
// dari role AKTIF + cache miss→hit (ADR-0011); (b) DoD #4 — login HTTP terbitkan JWT, request /auth/me
// ber-bearer → ICurrentUser identitas NYATA (adminId, BUKAN anonymous/SYSTEM, ADR-0016/0027). Validasi
// token offline pakai key ephemeral yang SAMA dengan issuer (single-process konsisten).
[Collection(PostgresCollection.Name)]
public sealed class AuthHostTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GetUser_grpc_assembles_active_role_permissions_then_serves_from_cache()
    {
        var connection = await fixture.CreateDatabaseAsync();
        await using var factory = new AuthFactory(connection);
        var adminId = await MigrateSeedAndGetAdminIdAsync(factory);

        var channel = GrpcChannel.ForAddress(factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler(),
        });
        var client = new AuthReadApi.AuthReadApiClient(channel);

        // (1) MISS → reader EF rakit role ADMIN + permission catalog → populate cache
        var first = await client.GetUserAsync(new GetUserRequest { UserId = adminId.ToString() });
        Assert.Equal("admin", first.Username);
        Assert.Contains("ADMIN", first.RoleCodes);
        Assert.Contains("Inbound.PostGR", first.PermissionCodes);

        // hapus admin di DB TANPA invalidasi cache (TTL-first) — uji cache-hit lewat gRPC
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            db.Users.Remove(await db.Users.SingleAsync(user => user.Username == "admin"));
            await db.SaveChangesAsync();
        }

        // (2) HIT → cache → masih "admin" walau row sudah hilang di DB
        var second = await client.GetUserAsync(new GetUserRequest { UserId = adminId.ToString() });
        Assert.Equal("admin", second.Username);
    }

    [Fact]
    public async Task Login_then_me_returns_real_identity_not_anonymous()
    {
        var connection = await fixture.CreateDatabaseAsync();
        await using var factory = new AuthFactory(connection);
        var adminId = await MigrateSeedAndGetAdminIdAsync(factory);
        var http = factory.CreateClient();

        // tanpa token: /auth/me = anonymous (kontras)
        var anon = await (await http.GetAsync("/auth/me")).Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal("anonymous", anon!.UserId);

        // login → access JWT
        var loginResponse = await http.PostAsJsonAsync("/auth/login", new { username = "admin", password = "ChangeMe123!" });
        loginResponse.EnsureSuccessStatusCode();
        var tokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // /auth/me ber-bearer → identitas NYATA (adminId), bukan anonymous/SYSTEM
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var me = await (await http.GetAsync("/auth/me")).Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(adminId.ToString(), me!.UserId);
    }

    private static async Task<Guid> MigrateSeedAndGetAdminIdAsync(AuthFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AuthSeeder>().SeedAsync();
        var admin = await db.Users.SingleAsync(user => user.Username == "admin");
        return admin.Id.Value;
    }

    // WebApplicationFactory Auth host (Program public partial) — override connection string ke test DB
    private sealed class AuthFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:authdb", connectionString);
    }

    private sealed record LoginResponse(string AccessToken, string RefreshToken);

    private sealed record MeResponse(string UserId);
}
