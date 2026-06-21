using Wms.Inventory.Domain;

namespace Wms.Inventory.Domain.UnitTests;

// What: behavioral fitness untuk aggregate Stock (ADR-0026)
// Why: invariant factory CreateOnHand + state awal OnHand.
public class StockTests
{
    private static readonly Guid Gr = Guid.NewGuid();

    [Fact]
    public void CreateOnHand_with_valid_data_succeeds_as_onhand()
    {
        var result = Stock.CreateOnHand(StockId.New(), "WH-JKT", "SKU-1", 10, Gr);

        Assert.True(result.IsSuccess);
        Assert.Equal(StockStatus.OnHand, result.Value.Status);
        Assert.Equal("SKU-1", result.Value.Sku);
        Assert.Equal(10, result.Value.Quantity);
        Assert.Equal(Gr, result.Value.SourceGoodsReceiptId);
    }

    [Fact]
    public void CreateOnHand_with_non_positive_quantity_fails()
    {
        var result = Stock.CreateOnHand(StockId.New(), "WH-JKT", "SKU-1", 0, Gr);

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.NonPositiveQuantity, result.Error);
    }

    [Fact]
    public void CreateOnHand_with_blank_sku_fails()
    {
        var result = Stock.CreateOnHand(StockId.New(), "WH-JKT", " ", 10, Gr);

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.MissingSku, result.Error);
    }
}
