using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;
using Wms.MasterData.Infrastructure.DependencyInjection;
using Wms.MasterData.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.MasterData.IntegrationTests;

// What: integration test read-side list MasterData — paging (PagedResult) + filter (isActive / warehouse /
// type) atas Postgres nyata. Why: membuktikan (1) ListProductsAsync mem-paginate (Skip/Take) DAN melaporkan
// TotalCount seluruh match (bukan ukuran page); (2) filter isActive melihat baris inactive (targeted bypass,
// ADR-0014) lalu menyaringnya; (3) ListLocationsAsync menyaring per warehouseId + type sebelum paging.
[Collection(PostgresCollection.Name)]
public sealed class MasterDataReaderListTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ListProducts_paginates_and_reports_total_across_all_matches()
    {
        await using var sp = await BuildAsync();
        for (var i = 0; i < 3; i++)
            await SeedProductAsync(sp, $"SKU-{i}", $"Product {i}", "box");

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IMasterDataReader>();

        var firstPage = await reader.ListProductsAsync(page: 1, pageSize: 2, isActive: null, search: null);
        Assert.Equal(3, firstPage.TotalCount);   // total seluruh match, bukan ukuran page
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(1, firstPage.Page);
        Assert.True(firstPage.HasNextPage);

        var secondPage = await reader.ListProductsAsync(page: 2, pageSize: 2, isActive: null, search: null);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Single(secondPage.Items);          // sisa 1 dari 3
        Assert.False(secondPage.HasNextPage);
    }

    [Fact]
    public async Task ListProducts_isActive_filter_distinguishes_active_from_inactive()
    {
        await using var sp = await BuildAsync();
        await SeedProductAsync(sp, "ACTIVE-1", "Active One", "box");
        await SeedProductAsync(sp, "GONE-1", "Gone One", "box");
        await DeactivateProductAsync(sp, "GONE-1");

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IMasterDataReader>();

        // isActive=true → hanya yang aktif
        var actives = await reader.ListProductsAsync(page: 1, pageSize: 20, isActive: true, search: null);
        Assert.Single(actives.Items);
        Assert.Equal("ACTIVE-1", actives.Items[0].Sku);
        Assert.True(actives.Items[0].IsActive);

        // isActive=false → hanya yang inactive (targeted bypass melihatnya, ADR-0014)
        var inactives = await reader.ListProductsAsync(page: 1, pageSize: 20, isActive: false, search: null);
        Assert.Single(inactives.Items);
        Assert.Equal("GONE-1", inactives.Items[0].Sku);
        Assert.False(inactives.Items[0].IsActive);

        // isActive=null → kedua-duanya
        var all = await reader.ListProductsAsync(page: 1, pageSize: 20, isActive: null, search: null);
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task ListLocations_filters_by_warehouse_and_type()
    {
        await using var sp = await BuildAsync();
        var warehouseId = Guid.NewGuid();
        var otherWarehouse = Guid.NewGuid();
        await SeedLocationAsync(sp, warehouseId, LocationType.ReceivingArea, "REC-01");
        await SeedLocationAsync(sp, warehouseId, LocationType.Rack, "RACK-01");
        await SeedLocationAsync(sp, otherWarehouse, LocationType.ReceivingArea, "OTHER-REC");

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IMasterDataReader>();

        // warehouse filter → 2 di warehouseId, OTHER-REC ter-exclude
        var byWarehouse = await reader.ListLocationsAsync(
            page: 1, pageSize: 20, warehouseId: warehouseId, type: null, isActive: null);
        Assert.Equal(2, byWarehouse.TotalCount);

        // warehouse + type filter → hanya REC-01
        var byType = await reader.ListLocationsAsync(
            page: 1, pageSize: 20, warehouseId: warehouseId, type: LocationType.ReceivingArea, isActive: null);
        Assert.Single(byType.Items);
        Assert.Equal("REC-01", byType.Items[0].Code);
        Assert.Equal(nameof(LocationType.ReceivingArea), byType.Items[0].Type);   // Type=enum→string
    }

    // ---- harness (mirror CacheAndSoftDeleteTests / GoodsReceiptReaderTests) ----

    private async Task<ServiceProvider> BuildAsync()
    {
        var sp = new ServiceCollection()
            .AddLogging()
            .AddMasterDataInfrastructure(await fixture.CreateDatabaseAsync())
            .AddLocalCaching()
            .BuildServiceProvider();

        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MasterDataDbContext>().Database.EnsureCreatedAsync();
        return sp;
    }

    private static async Task SeedProductAsync(IServiceProvider sp, string sku, string name, string uom)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        db.Products.Add(Product.Create(sku, name, uom, false, false, false, null).Value);
        await db.SaveChangesAsync();
    }

    private static async Task DeactivateProductAsync(IServiceProvider sp, string sku)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        var product = await db.Products.SingleAsync(p => p.Id == new ProductId(sku));
        Assert.True(product.Deactivate().IsSuccess);
        await db.SaveChangesAsync();
    }

    private static async Task SeedLocationAsync(IServiceProvider sp, Guid warehouseId, LocationType type, string code)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        db.Locations.Add(Location.Create(LocationId.New(), new WarehouseId(warehouseId), type, code).Value);
        await db.SaveChangesAsync();
    }
}
