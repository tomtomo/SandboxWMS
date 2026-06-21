using Wms.Outbound.Domain;

namespace Wms.Outbound.Domain.UnitTests;

// What: behavioral fitness untuk aggregate OutboundOrder — lifecycle New→InProgress→Closed (overview §C, ADR-0026)
// Why: order eksternal masuk sebagai New (orderLines di-snapshot, ADR-0014); masuk wave → InProgress; wave
// dispatch → Closed. Tiap transisi punya prasyarat state yang ditegakkan domain via Result (no-throw, FF#7).
public class OutboundOrderTests
{
    private static IReadOnlyList<OrderLineInput> Lines() =>
        [new OrderLineInput("SKU-1", 10, "carton"), new OrderLineInput("SKU-2", 5, "carton")];

    private static OutboundOrder New() =>
        OutboundOrder.Create(OutboundOrderId.New(), "CUST-1", "Jl. Merdeka 1", Lines()).Value;

    [Fact]
    public void Create_with_valid_data_succeeds_as_new()
    {
        var result = OutboundOrder.Create(OutboundOrderId.New(), "CUST-1", "Jl. Merdeka 1", Lines());

        Assert.True(result.IsSuccess);
        Assert.Equal(OutboundOrderStatus.New, result.Value.Status);
        Assert.Equal("CUST-1", result.Value.CustomerId);
        Assert.Equal("Jl. Merdeka 1", result.Value.ShipTo);
        Assert.Null(result.Value.WaveId);
    }

    [Fact]
    public void Create_snapshots_order_lines()
    {
        var order = New();

        Assert.Collection(order.OrderLines,
            line => { Assert.Equal("SKU-1", line.Sku); Assert.Equal(10, line.Qty); Assert.Equal("carton", line.Uom); },
            line => { Assert.Equal("SKU-2", line.Sku); Assert.Equal(5, line.Qty); Assert.Equal("carton", line.Uom); });
    }

    [Fact]
    public void Create_with_blank_customer_fails()
    {
        var result = OutboundOrder.Create(OutboundOrderId.New(), " ", "Jl. Merdeka 1", Lines());

        Assert.True(result.IsFailure);
        Assert.Equal(OutboundOrderErrors.MissingCustomer, result.Error);
    }

    [Fact]
    public void Create_with_blank_shipto_fails()
    {
        var result = OutboundOrder.Create(OutboundOrderId.New(), "CUST-1", " ", Lines());

        Assert.True(result.IsFailure);
        Assert.Equal(OutboundOrderErrors.MissingShipTo, result.Error);
    }

    [Fact]
    public void Create_with_no_lines_fails()
    {
        var result = OutboundOrder.Create(OutboundOrderId.New(), "CUST-1", "Jl. Merdeka 1", []);

        Assert.True(result.IsFailure);
        Assert.Equal(OutboundOrderErrors.NoOrderLines, result.Error);
    }

    [Fact]
    public void Create_with_non_positive_quantity_fails()
    {
        var result = OutboundOrder.Create(
            OutboundOrderId.New(), "CUST-1", "Jl. Merdeka 1", [new OrderLineInput("SKU-1", 0, "carton")]);

        Assert.True(result.IsFailure);
        Assert.Equal(OutboundOrderErrors.NonPositiveQuantity, result.Error);
    }

    [Fact]
    public void Create_with_blank_uom_fails()
    {
        var result = OutboundOrder.Create(
            OutboundOrderId.New(), "CUST-1", "Jl. Merdeka 1", [new OrderLineInput("SKU-1", 10, " ")]);

        Assert.True(result.IsFailure);
        Assert.Equal(OutboundOrderErrors.MissingUom, result.Error);
    }

    [Fact]
    public void PlaceInWave_moves_new_to_inprogress_with_wave()
    {
        var order = New();
        var waveId = Guid.NewGuid();

        var result = order.PlaceInWave(waveId);

        Assert.True(result.IsSuccess);
        Assert.Equal(OutboundOrderStatus.InProgress, order.Status);
        Assert.Equal(waveId, order.WaveId);
    }

    [Fact]
    public void PlaceInWave_when_already_inprogress_is_illegal()
    {
        var order = New();
        order.PlaceInWave(Guid.NewGuid());
        var firstWave = order.WaveId;

        var result = order.PlaceInWave(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(OutboundOrderErrors.InvalidWaveAssignment, result.Error);
        Assert.Equal(firstWave, order.WaveId); // tak berubah
    }

    [Fact]
    public void Close_moves_inprogress_to_closed()
    {
        var order = New();
        order.PlaceInWave(Guid.NewGuid());

        var result = order.Close();

        Assert.True(result.IsSuccess);
        Assert.Equal(OutboundOrderStatus.Closed, order.Status);
    }

    [Fact]
    public void Close_from_new_is_illegal()
    {
        var order = New(); // belum masuk wave

        var result = order.Close();

        Assert.True(result.IsFailure);
        Assert.Equal(OutboundOrderErrors.InvalidClose, result.Error);
        Assert.Equal(OutboundOrderStatus.New, order.Status); // tak berubah
    }
}
