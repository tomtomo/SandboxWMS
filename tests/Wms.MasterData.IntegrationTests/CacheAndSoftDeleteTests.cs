using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Application.ReadModels;
using Wms.MasterData.Domain;
using Wms.MasterData.Infrastructure.DependencyInjection;
using Wms.MasterData.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.MasterData.IntegrationTests;

// What: integration test cache-aside + soft-delete + migration MasterData (DoD Phase 04a) atas Postgres
// nyata. Why: membuktikan (1) migration InitialMasterData apply bersih; (2) cache-aside TTL-first
// (ADR-0011): MISS → populate dari authority, HIT → served dari cache walau DB berubah; (3) global
// soft-delete filter (ADR-0014) menyembunyikan Product inactive, sedang TARGETED bypass (flag
// IncludeInactive) tetap melihatnya — bukan blanket IgnoreQueryFilters.
[Collection(PostgresCollection.Name)]
public sealed class CacheAndSoftDeleteTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Migration_applies_cleanly_on_real_postgres()
    {
        await using var sp = new ServiceCollection()
            .AddLogging()
            .AddMasterDataInfrastructure(await fixture.CreateDatabaseAsync())
            .BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();

        await db.Database.MigrateAsync();                                   // apply InitialMasterData
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());        // semua migration ter-apply
    }

    [Fact]
    public async Task Cache_aside_miss_populates_then_hit_serves_from_cache_despite_db_change()
    {
        await using var sp = await BuildAsync();
        await SeedProductAsync(sp, "WIDGET", uom: "box");

        // MISS → populate cache dari authority
        var first = await ReadCachedAsync(sp, "WIDGET");
        Assert.NotNull(first);
        Assert.Equal("box", first!.Uom);

        // ubah authority (hapus row) TANPA invalidasi cache (TTL-first, tak ada event invalidation)
        await DeleteProductAsync(sp, "WIDGET");

        // HIT → served dari cache (masih "box") walau row sudah hilang di DB → bukti cache-hit
        var second = await ReadCachedAsync(sp, "WIDGET");
        Assert.NotNull(second);
        Assert.Equal("box", second!.Uom);
    }

    [Fact]
    public async Task Soft_delete_hides_product_but_targeted_bypass_still_sees_it()
    {
        await using var sp = await BuildAsync();
        await SeedProductAsync(sp, "GADGET", uom: "piece");

        // soft-delete TANPA membaca dulu → cache tetap kosong (tak mengaburkan uji global filter)
        await DeactivateProductAsync(sp, "GADGET");

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IMasterDataReader>();  // cached decorator (public)

        // cache miss → EF global filter menyembunyikan yang inactive (null tak di-cache)
        Assert.Null(await reader.GetProductAsync("GADGET"));
        // targeted bypass (flag IncludeInactive, pass-through uncached) tetap melihat — tanpa mematikan filter lain
        Assert.NotNull(await reader.GetProductIncludingInactiveAsync("GADGET"));
    }

    [Fact]
    public async Task GetDefaultLocation_resolves_active_location_by_warehouse_and_type()
    {
        await using var sp = await BuildAsync();
        var warehouseId = Guid.NewGuid();
        await SeedLocationAsync(sp, warehouseId, LocationType.ReceivingArea, "REC-01");
        await SeedLocationAsync(sp, warehouseId, LocationType.QuarantineArea, "QC-A");

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IMasterDataReader>();   // cached decorator

        Assert.Equal("REC-01", (await reader.GetDefaultLocationAsync(warehouseId, LocationType.ReceivingArea))!.Code);
        Assert.Equal("QC-A", (await reader.GetDefaultLocationAsync(warehouseId, LocationType.QuarantineArea))!.Code);
        // warehouse tanpa lokasi tipe itu → null (NotFound) → consumer loud-fail (MissingDefaultLocation)
        Assert.Null(await reader.GetDefaultLocationAsync(Guid.NewGuid(), LocationType.ReceivingArea));
    }

    // ---- harness ----

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

    private static async Task SeedProductAsync(IServiceProvider sp, string sku, string uom)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        db.Products.Add(Product.Create(sku, sku, uom, false, false, false, null).Value);
        await db.SaveChangesAsync();
    }

    private static async Task SeedLocationAsync(IServiceProvider sp, Guid warehouseId, LocationType type, string code)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        db.Locations.Add(Location.Create(LocationId.New(), new WarehouseId(warehouseId), type, code).Value);
        await db.SaveChangesAsync();
    }

    private static async Task DeleteProductAsync(IServiceProvider sp, string sku)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        db.Products.Remove(await db.Products.SingleAsync(p => p.Id == new ProductId(sku)));
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

    // cached IMasterDataReader (decorator) — ICacheStore singleton shared lintas scope (cache hidup)
    private static async Task<ProductReadModel?> ReadCachedAsync(IServiceProvider sp, string sku)
    {
        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IMasterDataReader>();
        return await reader.GetProductAsync(sku);
    }
}
