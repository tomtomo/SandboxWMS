using Grpc.Net.Client;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Resilience;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Application.DependencyInjection;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Inbound.Infrastructure.Persistence;
using Wms.MasterData.Domain;
using Wms.MasterData.Grpc;
using Wms.MasterData.Infrastructure.Persistence;
using Wms.TestSupport;

namespace Wms.MasterData.IntegrationTests;

// What: integration test gRPC read-API REAL transport + Inbound snapshot (DoD Phase 04a) atas Postgres
// Why: membuktikan DoD inti — Inbound CreateGoodsReceipt men-snapshot uom dari MasterData VIA gRPC
// (server in-proc WebApplicationFactory + channel HTTP/2), dengan cache miss→populate lalu hit→served
// dari cache MasterData; sku tak dikenal → gRPC NotFound → handler gagal (UnknownProduct). Membuktikan
// rantai penuh: GrpcProductCatalog (ACL + resilience pipeline) → server gRPC → cache-aside reader.
[Collection(PostgresCollection.Name)]
public sealed class GrpcReadApiIntegrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Inbound_snapshots_uom_via_grpc_with_cache_miss_then_hit_and_unknown_fails()
    {
        var masterDataConnection = await fixture.CreateDatabaseAsync();
        await using var factory = new MasterDataFactory(masterDataConnection);

        // migrate + seed Product "WIDGET" (uom "box") di authority MasterData
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
            await db.Database.MigrateAsync();
            db.Products.Add(Product.Create("WIDGET", "Widget", "box", false, false, false, null).Value);
            await db.SaveChangesAsync();
        }

        // channel gRPC ke server in-proc (TestServer handler → HTTP/2 in-memory)
        var channel = GrpcChannel.ForAddress(factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler(),
        });

        await using var inbound = BuildInbound(await fixture.CreateDatabaseAsync(), channel);
        using (var scope = inbound.CreateScope())
            await scope.ServiceProvider.GetRequiredService<InboundDbContext>().Database.EnsureCreatedAsync();

        // (1) MISS → gRPC → MasterData reader → DB "box" → populate cache MasterData
        Assert.Equal("box", (await ResolveAsync(inbound, "WIDGET"))!.Uom);

        // hapus row di authority TANPA invalidasi cache (TTL-first) — uji cache-hit lewat gRPC
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
            db.Products.Remove(await db.Products.SingleAsync());
            await db.SaveChangesAsync();
        }

        // (2) HIT → gRPC → cache MasterData → masih "box" walau row sudah hilang di DB
        Assert.Equal("box", (await ResolveAsync(inbound, "WIDGET"))!.Uom);

        // (3) CreateGoodsReceipt men-snapshot uom VIA gRPC (cache hit → "box") → sukses
        using (var scope = inbound.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var create = await sender.Send(new CreateGoodsReceiptCommand(
                "WH-JKT", [new CreateGoodsReceiptLine("WIDGET", 10)]));
            Assert.True(create.IsSuccess);
        }

        // (4) sku tak dikenal → gRPC NotFound → catalog null → handler gagal UnknownProduct
        using (var scope = inbound.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var create = await sender.Send(new CreateGoodsReceiptCommand(
                "WH-JKT", [new CreateGoodsReceiptLine("NOPE", 5)]));
            Assert.True(create.IsFailure);
            Assert.Equal("inbound.unknown_product", create.Error.Code);
        }
    }

    // provider Inbound dengan adapter gRPC REAL (GrpcProductCatalog) + resilience pipeline + client channel
    private static ServiceProvider BuildInbound(string inboundConnection, GrpcChannel channel) =>
        new ServiceCollection()
            .AddLogging()
            .AddInboundApplication()
            .AddInboundInfrastructure(inboundConnection)
            .AddGrpcResiliencePipeline()
            .AddSingleton(new MasterDataReadApi.MasterDataReadApiClient(channel))
            .AddMasterDataProductCatalog()
            .BuildServiceProvider();

    private static async Task<ProductSnapshot?> ResolveAsync(IServiceProvider inbound, string sku)
    {
        using var scope = inbound.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProductCatalog>().GetProductAsync(sku);
    }

    // WebApplicationFactory MasterData host (Program public partial) — override connection string ke test DB
    private sealed class MasterDataFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:masterdatadb", connectionString);
    }
}
