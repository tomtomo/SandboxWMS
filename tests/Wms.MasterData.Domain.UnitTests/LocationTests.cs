using Wms.MasterData.Domain;

namespace Wms.MasterData.Domain.UnitTests;

// What: unit test aggregate Location (DDD invariant + soft-delete, ADR-0014)
// Why: Location = authority lokasi fisik (overview §D) yang merefer Warehouse by-id (Vernon IDDD).
public sealed class LocationTests
{
    private static readonly WarehouseId Wh = WarehouseId.New();

    [Fact]
    public void Create_succeeds_with_valid_fields()
    {
        var result = Location.Create(LocationId.New(), Wh, LocationType.Rack, "RACK-B12-03");

        Assert.True(result.IsSuccess);
        Assert.Equal(Wh, result.Value.WarehouseId);
        Assert.Equal(LocationType.Rack, result.Value.Type);
        Assert.Equal("RACK-B12-03", result.Value.Code);
        Assert.True(result.Value.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_code_blank(string code)
    {
        var result = Location.Create(LocationId.New(), Wh, LocationType.Rack, code);

        Assert.True(result.IsFailure);
        Assert.Equal(LocationErrors.MissingCode.Code, result.Error.Code);
    }

    [Fact]
    public void Create_fails_when_warehouse_empty()
    {
        var result = Location.Create(LocationId.New(), new WarehouseId(Guid.Empty), LocationType.Rack, "RACK-1");

        Assert.True(result.IsFailure);
        Assert.Equal(LocationErrors.MissingWarehouse.Code, result.Error.Code);
    }

    [Fact]
    public void Deactivate_then_activate_round_trips_isactive()
    {
        var location = Location.Create(LocationId.New(), Wh, LocationType.ReceivingArea, "REC-01").Value;

        Assert.True(location.Deactivate().IsSuccess);
        Assert.False(location.IsActive);
        Assert.True(location.Activate().IsSuccess);
        Assert.True(location.IsActive);
    }

    [Fact]
    public void Deactivate_fails_when_already_inactive()
    {
        var location = Location.Create(LocationId.New(), Wh, LocationType.ReceivingArea, "REC-01").Value;
        location.Deactivate();

        Assert.Equal(LocationErrors.AlreadyInactive.Code, location.Deactivate().Error.Code);
    }
}
