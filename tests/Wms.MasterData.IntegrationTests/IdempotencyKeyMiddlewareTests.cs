using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.MasterData.Infrastructure.Persistence;
using Wms.TestSupport;

namespace Wms.MasterData.IntegrationTests;

// What: integration test Idempotency-Key middleware (ADR-0032) atas MasterData host REAL (Postgres) — reference host
// Why: membuktikan vertical penuh — (1) migration api_idempotency APPLY BERSIH via MigrateAsync (termasuk
// kolom xmin no-op WP-6/ADR-0031), (2) POST mutating ber-Idempotency-Key → retry key SAMA me-REPLAY response
// asli TANPA membuat resource kedua (retry-safe, EIP Idempotent Receiver), (3) tanpa header → tiap POST buat
// resource baru (control: middleware hanya bertindak saat header ada). Server in-proc WebApplicationFactory.
[Collection(PostgresCollection.Name)]
public sealed class IdempotencyKeyMiddlewareTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Repeated_post_with_same_idempotency_key_replays_response_and_creates_once()
    {
        var connection = await fixture.CreateDatabaseAsync();
        await using var factory = new MasterDataFactory(connection);

        // MigrateAsync menerapkan migration NYATA (InitialMasterData + AddApiIdempotency) → buktikan apply bersih
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<MasterDataDbContext>().Database.MigrateAsync();

        var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString();

        var first = await PostWarehouseAsync(client, key, "DC Cakung", "Jl. Cakung No.1");
        var second = await PostWarehouseAsync(client, key, "DC Cakung", "Jl. Cakung No.1"); // retry, key SAMA

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        // response asli di-replay verbatim (id sama) — bukan resource baru
        var firstBody = await first.Content.ReadAsStringAsync();
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.Equal(firstBody, secondBody);

        // INVARIAN retry-safety: tepat SATU warehouse dibuat (retry tak menduplikasi)
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        Assert.Equal(1, await db.Warehouses.CountAsync());
    }

    [Fact]
    public async Task Posts_without_idempotency_key_each_create_a_resource()
    {
        var connection = await fixture.CreateDatabaseAsync();
        await using var factory = new MasterDataFactory(connection);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<MasterDataDbContext>().Database.MigrateAsync();

        var client = factory.CreateClient();

        // tanpa header Idempotency-Key → middleware pass-through → tiap POST membuat resource baru (control)
        var a = await PostWarehouseAsync(client, idempotencyKey: null, "WH-A", "addr-a");
        var b = await PostWarehouseAsync(client, idempotencyKey: null, "WH-B", "addr-b");
        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);

        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        Assert.Equal(2, await db.Warehouses.CountAsync());
    }

    private static async Task<HttpResponseMessage> PostWarehouseAsync(
        HttpClient client, string? idempotencyKey, string name, string address)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/warehouses")
        {
            Content = JsonContent.Create(new { name, address }),
        };
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    // WebApplicationFactory MasterData host (Program public partial) — override connection string ke test DB
    private sealed class MasterDataFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:masterdatadb", connectionString);
    }
}
