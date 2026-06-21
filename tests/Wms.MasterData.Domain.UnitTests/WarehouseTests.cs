using Wms.MasterData.Domain;

namespace Wms.MasterData.Domain.UnitTests;

// What: unit test aggregate Warehouse (DDD invariant + soft-delete lifecycle, ADR-0014)
// Why: Warehouse = authority master (overview §D) dgn lifecycle sederhana isActive — factory
// menegakkan invariant (name/address wajib) dan transisi soft-delete dijaga guard (no double).
public sealed class WarehouseTests
{
    [Fact]
    public void Create_succeeds_with_valid_name_and_address()
    {
        var result = Warehouse.Create(WarehouseId.New(), "DC Jakarta Cakung", "Jl. Cakung Cilincing No.1");

        Assert.True(result.IsSuccess);
        Assert.Equal("DC Jakarta Cakung", result.Value.Name);
        Assert.Equal("Jl. Cakung Cilincing No.1", result.Value.Address);
        Assert.True(result.Value.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_name_blank(string name)
    {
        var result = Warehouse.Create(WarehouseId.New(), name, "addr");

        Assert.True(result.IsFailure);
        Assert.Equal(WarehouseErrors.MissingName.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_address_blank(string address)
    {
        var result = Warehouse.Create(WarehouseId.New(), "WH", address);

        Assert.True(result.IsFailure);
        Assert.Equal(WarehouseErrors.MissingAddress.Code, result.Error.Code);
    }

    [Fact]
    public void Deactivate_sets_inactive()
    {
        var warehouse = Warehouse.Create(WarehouseId.New(), "WH", "addr").Value;

        var result = warehouse.Deactivate();

        Assert.True(result.IsSuccess);
        Assert.False(warehouse.IsActive);
    }

    [Fact]
    public void Deactivate_fails_when_already_inactive()
    {
        var warehouse = Warehouse.Create(WarehouseId.New(), "WH", "addr").Value;
        warehouse.Deactivate();

        var result = warehouse.Deactivate();

        Assert.True(result.IsFailure);
        Assert.Equal(WarehouseErrors.AlreadyInactive.Code, result.Error.Code);
    }

    [Fact]
    public void Activate_restores_active()
    {
        var warehouse = Warehouse.Create(WarehouseId.New(), "WH", "addr").Value;
        warehouse.Deactivate();

        var result = warehouse.Activate();

        Assert.True(result.IsSuccess);
        Assert.True(warehouse.IsActive);
    }

    [Fact]
    public void Activate_fails_when_already_active()
    {
        var warehouse = Warehouse.Create(WarehouseId.New(), "WH", "addr").Value;

        var result = warehouse.Activate();

        Assert.True(result.IsFailure);
        Assert.Equal(WarehouseErrors.AlreadyActive.Code, result.Error.Code);
    }
}
