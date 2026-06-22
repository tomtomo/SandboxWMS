using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Application.DependencyInjection;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Inbound.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Inbound.IntegrationTests;

// What: integration test read-side GoodsReceipt — paging (PagedResult) atas Postgres nyata
// Why: membuktikan IGoodsReceiptReader.ListAsync mem-paginate (Skip/Take) DAN melaporkan TotalCount
// dari seluruh match (bukan hanya page) — kontrak pagination yang dipakai dashboard. Filter warehouse
// menyaring sebelum paging.
[Collection(PostgresCollection.Name)]
public sealed class GoodsReceiptReaderTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ListAsync_paginates_items_and_reports_total_across_all_matches()
    {
        var provider = await BuildAsync();
        await using var _provider = provider;

        // seed 3 GR di WH-PAGE (+ 1 GR di gudang lain → harus ter-exclude oleh filter)
        for (var i = 0; i < 3; i++)
            await SendAsync(provider, new CreateGoodsReceiptCommand("WH-PAGE",
                [new CreateGoodsReceiptLine("SKU-A", 5)]));
        await SendAsync(provider, new CreateGoodsReceiptCommand("WH-OTHER",
            [new CreateGoodsReceiptLine("SKU-A", 5)]));

        using var scope = provider.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IGoodsReceiptReader>();

        var firstPage = await reader.ListAsync(warehouseId: "WH-PAGE", page: 1, pageSize: 2);
        Assert.Equal(3, firstPage.TotalCount);   // total seluruh match, bukan ukuran page
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(1, firstPage.Page);
        Assert.True(firstPage.HasNextPage);

        var secondPage = await reader.ListAsync(warehouseId: "WH-PAGE", page: 2, pageSize: 2);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Single(secondPage.Items);         // sisa 1 dari 3
        Assert.False(secondPage.HasNextPage);
    }

    private async Task<ServiceProvider> BuildAsync()
    {
        var objectRoot = Path.Combine(Path.GetTempPath(), "wms-inbound-rdr-" + Guid.NewGuid().ToString("N"));
        var provider = new ServiceCollection()
            .AddLogging()
            .AddInboundApplication()
            .AddInboundProductCatalogStub()
            .AddInboundInfrastructure(await fixture.CreateDatabaseAsync())
            .AddLocalMessaging()
            .AddLocalAuditing()
            .AddLocalObjectStore(objectRoot)
            .BuildServiceProvider();

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<InboundDbContext>().Database.EnsureCreatedAsync();
        return provider;
    }

    private static async Task<TResponse> SendAsync<TResponse>(IServiceProvider provider, IRequest<TResponse> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
