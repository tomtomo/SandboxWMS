using Wms.BuildingBlocks.Domain.Results;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Domain.UnitTests;

// What: unit test aggregate Product (DDD invariant + soft-delete, ADR-0014)
// Why: Product = authority katalog SKU (overview §D); uom/batchTrackingRequired di-snapshot core
// (ADR-0014) → factory wajib menegakkan field-field kritikal itu valid sebelum dipublish read-API.
public sealed class ProductTests
{
    private static Result<Product> CreateValid(string sku = "SKU-001") =>
        Product.Create(sku, "Widget", "carton", batchTrackingRequired: true,
            expiryTrackingRequired: true, qcRequiredOnReceipt: false, shelfLifeDays: 365);

    [Fact]
    public void Create_succeeds_and_captures_fields()
    {
        var product = CreateValid().Value;

        Assert.Equal("SKU-001", product.Id.Value);
        Assert.Equal("Widget", product.Name);
        Assert.Equal("carton", product.Uom);
        Assert.True(product.BatchTrackingRequired);
        Assert.True(product.ExpiryTrackingRequired);
        Assert.False(product.QcRequiredOnReceipt);
        Assert.Equal(365, product.ShelfLifeDays);
        Assert.True(product.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_sku_blank(string sku)
    {
        var result = Product.Create(sku, "Widget", "carton", false, false, false, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.MissingSku.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_name_blank(string name)
    {
        var result = Product.Create("SKU-001", name, "carton", false, false, false, null);

        Assert.Equal(ProductErrors.MissingName.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_uom_blank(string uom)
    {
        var result = Product.Create("SKU-001", "Widget", uom, false, false, false, null);

        Assert.Equal(ProductErrors.MissingUom.Code, result.Error.Code);
    }

    [Fact]
    public void Create_allows_null_shelf_life()
    {
        var result = Product.Create("SKU-001", "Widget", "carton", false, false, false, shelfLifeDays: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ShelfLifeDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_fails_when_shelf_life_not_positive(int days)
    {
        var result = Product.Create("SKU-001", "Widget", "carton", false, false, false, days);

        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.InvalidShelfLife.Code, result.Error.Code);
    }

    [Fact]
    public void Deactivate_then_activate_round_trips()
    {
        var product = CreateValid().Value;

        Assert.True(product.Deactivate().IsSuccess);
        Assert.False(product.IsActive);
        Assert.True(product.Activate().IsSuccess);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void Deactivate_fails_when_already_inactive()
    {
        var product = CreateValid().Value;
        product.Deactivate();

        Assert.Equal(ProductErrors.AlreadyInactive.Code, product.Deactivate().Error.Code);
    }
}
