namespace Wms.Inbound.Domain.UnitTests;

// What: behavioral fitness untuk aggregate GoodsReceipt (ADR-0026)
// Why: memverifikasi invariant factory + emission policy (event hanya di-raise pada
// fakta sukses Confirm, tak ada di guard gagal) — dimensi yang tak terjangkau
// NetArchTest statik.
public class GoodsReceiptTests
{
    private static readonly GoodsReceiptLineInput[] OneLine = [new("SKU-1", 10)];

    private static GoodsReceipt NewInProgress() =>
        GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", OneLine).Value;

    [Fact]
    public void Create_with_valid_data_succeeds_and_is_in_progress()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", OneLine);

        Assert.True(result.IsSuccess);
        Assert.Equal(GoodsReceiptStatus.InProgress, result.Value.Status);
        Assert.Single(result.Value.Lines);
    }

    [Fact]
    public void Create_does_not_raise_domain_event()
    {
        // emission policy: pembuatan bukan "fakta bisnis" yang dipublish (ADR-0026)
        Assert.Empty(NewInProgress().DomainEvents);
    }

    [Fact]
    public void Create_without_lines_fails_validation()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", []);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.NoLines, result.Error);
    }

    [Fact]
    public void Create_with_non_positive_quantity_fails()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", [new("SKU-1", 0)]);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.NonPositiveQuantity, result.Error);
    }

    [Fact]
    public void Create_with_blank_sku_fails()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", [new(" ", 10)]);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.MissingSku, result.Error);
    }

    [Fact]
    public void Create_with_blank_warehouse_fails()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "  ", OneLine);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.MissingWarehouse, result.Error);
    }

    [Fact]
    public void Confirm_transitions_to_confirmed()
    {
        var gr = NewInProgress();

        var result = gr.Confirm();

        Assert.True(result.IsSuccess);
        Assert.Equal(GoodsReceiptStatus.Confirmed, gr.Status);
    }

    [Fact]
    public void Confirm_raises_GoodsReceiptConfirmed_carrying_lines()
    {
        var gr = NewInProgress();

        gr.Confirm();

        var evt = Assert.Single(gr.DomainEvents);
        var confirmed = Assert.IsType<GoodsReceiptConfirmed>(evt);
        Assert.Equal("WH-JKT", confirmed.WarehouseId);
        var line = Assert.Single(confirmed.Lines);
        Assert.Equal("SKU-1", line.Sku);
        Assert.Equal(10, line.Quantity);
    }

    [Fact]
    public void Confirm_twice_fails_and_raises_no_second_event()
    {
        var gr = NewInProgress();
        gr.Confirm();

        var second = gr.Confirm();

        Assert.True(second.IsFailure);
        Assert.Equal(GoodsReceiptErrors.AlreadyConfirmed, second.Error);
        Assert.Single(gr.DomainEvents); // tetap satu — guard gagal tak emit
    }
}
