using Microsoft.Extensions.DependencyInjection;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Inventory.IntegrationTests;

// What: integration test read-side Stock & PutawayTask — paging + filter atas Postgres nyata
// Why: membuktikan IStockReader/IPutawayTaskReader.ListAsync mem-paginate (Skip/Take) DAN melaporkan
// TotalCount dari seluruh match (bukan hanya page) — kontrak pagination dashboard. Filter (warehouse/
// sku/status; assignedTo/status) menyaring sebelum paging; Status di-flatten ke string.
[Collection(PostgresCollection.Name)]
public sealed class StockReaderTests(PostgresFixture fixture)
{
    private const string Warehouse = "WH-RDR";

    [Fact]
    public async Task Stocks_ListAsync_paginates_and_reports_total_across_all_matches()
    {
        await using var sp = await BuildInventoryAsync();

        // 3 Available SKU-A di WH-RDR + 1 di gudang lain (harus ter-exclude oleh filter warehouse)
        for (var i = 0; i < 3; i++)
            await SeedStockAsync(sp, Warehouse, "SKU-A", StockStatus.Available);
        await SeedStockAsync(sp, "WH-OTHER", "SKU-A", StockStatus.Available);

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IStockReader>();

        var firstPage = await reader.ListAsync(warehouseId: Warehouse, page: 1, pageSize: 2);
        Assert.Equal(3, firstPage.TotalCount);   // total seluruh match, bukan ukuran page
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(1, firstPage.Page);
        Assert.True(firstPage.HasNextPage);
        Assert.All(firstPage.Items, item => Assert.Equal("Available", item.Status)); // enum→string-name

        var secondPage = await reader.ListAsync(warehouseId: Warehouse, page: 2, pageSize: 2);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Single(secondPage.Items);         // sisa 1 dari 3
        Assert.False(secondPage.HasNextPage);
    }

    [Fact]
    public async Task Stocks_ListAsync_filters_by_status()
    {
        await using var sp = await BuildInventoryAsync();

        await SeedStockAsync(sp, Warehouse, "SKU-1", StockStatus.OnHand);
        await SeedStockAsync(sp, Warehouse, "SKU-2", StockStatus.Available);

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IStockReader>();

        var onHand = await reader.ListAsync(status: StockStatus.OnHand);
        var item = Assert.Single(onHand.Items);
        Assert.Equal("OnHand", item.Status);
        Assert.Equal("SKU-1", item.Sku);
    }

    [Fact]
    public async Task PutawayTasks_ListAsync_paginates_and_filters_by_assigned_to()
    {
        await using var sp = await BuildInventoryAsync();

        // 2 task assigned ke OP-1 + 1 task tanpa assignee (filter assignedTo harus meng-exclude null)
        var stockA = await SeedStockAsync(sp, Warehouse, "SKU-A", StockStatus.OnHand);
        var stockB = await SeedStockAsync(sp, Warehouse, "SKU-B", StockStatus.OnHand);
        var stockC = await SeedStockAsync(sp, Warehouse, "SKU-C", StockStatus.OnHand);
        await SeedPutawayTaskAsync(sp, stockA, assignedTo: "OP-1");
        await SeedPutawayTaskAsync(sp, stockB, assignedTo: "OP-1");
        await SeedPutawayTaskAsync(sp, stockC, assignedTo: null);

        using var scope = sp.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPutawayTaskReader>();

        var firstPage = await reader.ListAsync(assignedTo: "OP-1", page: 1, pageSize: 1);
        Assert.Equal(2, firstPage.TotalCount);   // hanya OP-1, null ter-exclude
        Assert.Single(firstPage.Items);
        Assert.True(firstPage.HasNextPage);
        Assert.All(firstPage.Items, item => Assert.Equal("Assigned", item.Status)); // enum→string-name
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

    // seed Stock pada state tertentu via domain factory+transisi (lalu persist), kembalikan id.
    private static async Task<Guid> SeedStockAsync(
        ServiceProvider sp, string warehouse, string sku, StockStatus target)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var stock = Stock.CreateOnHand(StockId.New(), warehouse, sku, "REC-01", "B1", null, 10, Guid.NewGuid()).Value;
        if (target is StockStatus.Available or StockStatus.Allocated or StockStatus.Picked)
            Assert.True(stock.Putaway("RACK-A1").IsSuccess);
        if (target is StockStatus.Allocated or StockStatus.Picked)
            Assert.True(stock.Allocate(Guid.NewGuid()).IsSuccess);
        if (target is StockStatus.Picked)
            Assert.True(stock.Pick(Guid.NewGuid(), "STG-1").IsSuccess);

        db.Stocks.Add(stock);
        await db.SaveChangesAsync();
        return stock.Id.Value;
    }

    private static async Task SeedPutawayTaskAsync(ServiceProvider sp, Guid stockId, string? assignedTo)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var task = PutawayTask.Assign(PutawayTaskId.New(), new StockId(stockId), "REC-01", "RACK-A1", assignedTo);
        db.PutawayTasks.Add(task);
        await db.SaveChangesAsync();
    }
}
