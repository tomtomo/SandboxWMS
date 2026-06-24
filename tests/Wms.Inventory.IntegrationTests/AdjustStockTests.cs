using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Application.Features.AdjustStock;
using Wms.Inventory.Domain;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Inventory.IntegrationTests;

// What: test koreksi manual kuantitas Stock — domain transition (no-throw) + handler (load→adjust→persist)
// Why: membuktikan invariant non-negatif ditegakkan di domain (Result.Failure, FF#7) DAN handler memuat
// aggregate, menerapkan transisi, lalu mem-persist kuantitas baru atas Postgres nyata; NotFound saat hilang.
[Collection(PostgresCollection.Name)]
public sealed class AdjustStockTests(PostgresFixture fixture)
{
    private const string Warehouse = "WH-ADJ";

    [Fact]
    public void Adjust_with_negative_quantity_fails_with_negative_quantity_error_and_keeps_state()
    {
        var stock = Stock.CreateOnHand(StockId.New(), Warehouse, "SKU-1", "REC-01", "B1", null, 10, Guid.NewGuid()).Value;

        var result = stock.Adjust(-1);

        Assert.True(result.IsFailure);
        Assert.Equal("stock.negative_quantity", result.Error.Code);
        Assert.Equal(10, stock.Quantity);   // state tak berubah saat gagal (no-throw)
    }

    [Fact]
    public void Adjust_with_non_negative_quantity_sets_quantity()
    {
        var stock = Stock.CreateOnHand(StockId.New(), Warehouse, "SKU-1", "REC-01", "B1", null, 10, Guid.NewGuid()).Value;

        Assert.True(stock.Adjust(7).IsSuccess);
        Assert.Equal(7, stock.Quantity);

        Assert.True(stock.Adjust(0).IsSuccess);   // nol valid (koreksi → habis)
        Assert.Equal(0, stock.Quantity);
    }

    [Fact]
    public async Task Handler_adjusts_existing_stock_and_persists_new_quantity()
    {
        await using var sp = await BuildInventoryAsync();
        var stockId = await SeedStockAsync(sp);

        using (var scope = sp.CreateScope())
        {
            var services = scope.ServiceProvider;
            var handler = new AdjustStockHandler(
                services.GetRequiredService<IStockRepository>(),
                services.GetRequiredService<IUnitOfWork>());
            var result = await handler.Handle(new AdjustStockCommand(stockId, 42), default);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        }

        using var verify = sp.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var persisted = await db.Stocks.SingleAsync(s => s.Id == new StockId(stockId));
        Assert.Equal(42, persisted.Quantity);
    }

    [Fact]
    public async Task Handler_returns_not_found_for_missing_stock()
    {
        await using var sp = await BuildInventoryAsync();

        using var scope = sp.CreateScope();
        var services = scope.ServiceProvider;
        var handler = new AdjustStockHandler(
            services.GetRequiredService<IStockRepository>(),
            services.GetRequiredService<IUnitOfWork>());

        var result = await handler.Handle(new AdjustStockCommand(Guid.NewGuid(), 5), default);

        Assert.True(result.IsFailure);
        Assert.Equal("stock.not_found", result.Error.Code);
    }

    // ---- harness ----

    private async Task<ServiceProvider> BuildInventoryAsync()
    {
        var sp = new ServiceCollection()
            .AddLogging()
            .AddInventoryInfrastructure(await fixture.CreateDatabaseAsync())
            .AddInventoryLocationCatalogStub()
            .AddLocalMessaging()
            .BuildServiceProvider();

        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.EnsureCreatedAsync();
        return sp;
    }

    private static async Task<Guid> SeedStockAsync(ServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var stock = Stock.CreateOnHand(StockId.New(), Warehouse, "SKU-1", "REC-01", "B1", null, 10, Guid.NewGuid()).Value;
        db.Stocks.Add(stock);
        await db.SaveChangesAsync();
        return stock.Id.Value;
    }
}
