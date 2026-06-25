using Wms.Inventory.Domain;

namespace Wms.Inventory.Domain.UnitTests;

// What: behavioral fitness untuk aggregate Stock — lifecycle penuh (ADR-0026, Phase 03b)
// Why: Stock ditransisikan oleh trigger event-driven berbeda (putaway, allocate, pick). Tiap
// transisi punya prasyarat state (legal/ilegal) yang ditegakkan domain via Result (no-throw, FF#7).
// Quarantine TIDAK boleh putaway (tak masuk rak reguler — overview §B catatan).
public class StockTests
{
    private static readonly Guid Gr = Guid.NewGuid();
    private static readonly DateOnly Expiry = new(2026, 12, 31);

    private static Stock OnHand() =>
        Stock.CreateOnHand(StockId.New(), "WH-JKT", "SKU-1", "REC-01", "B1", Expiry, 10, Gr).Value;

    private static Stock Available()
    {
        var stock = OnHand();
        stock.Putaway("RACK-A1");
        return stock;
    }

    [Fact]
    public void CreateOnHand_with_valid_data_succeeds_as_onhand()
    {
        var result = Stock.CreateOnHand(StockId.New(), "WH-JKT", "SKU-1", "REC-01", "B1", Expiry, 10, Gr);

        Assert.True(result.IsSuccess);
        Assert.Equal(StockStatus.OnHand, result.Value.Status);
        Assert.Equal("SKU-1", result.Value.Sku);
        Assert.Equal("REC-01", result.Value.LocationId);
        Assert.Equal("B1", result.Value.Batch);
        Assert.Equal(Expiry, result.Value.Expiry);
        Assert.Equal(10, result.Value.Quantity);
        Assert.Equal(Gr, result.Value.SourceGoodsReceiptId);
    }

    [Fact]
    public void CreateOnHand_allows_null_batch_and_expiry()
    {
        var result = Stock.CreateOnHand(StockId.New(), "WH-JKT", "SKU-1", "REC-01", null, null, 10, Gr);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Batch);
        Assert.Null(result.Value.Expiry);
    }

    [Fact]
    public void CreateOnHand_with_non_positive_quantity_fails()
    {
        var result = Stock.CreateOnHand(StockId.New(), "WH-JKT", "SKU-1", "REC-01", "B1", Expiry, 0, Gr);

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.NonPositiveQuantity, result.Error);
    }

    [Fact]
    public void CreateOnHand_with_blank_sku_fails()
    {
        var result = Stock.CreateOnHand(StockId.New(), "WH-JKT", " ", "REC-01", "B1", Expiry, 10, Gr);

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.MissingSku, result.Error);
    }

    [Fact]
    public void CreateOnHand_with_blank_location_fails()
    {
        var result = Stock.CreateOnHand(StockId.New(), "WH-JKT", "SKU-1", " ", "B1", Expiry, 10, Gr);

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.MissingLocation, result.Error);
    }

    [Fact]
    public void CreateQuarantine_with_valid_data_succeeds_as_quarantine()
    {
        var result = Stock.CreateQuarantine(StockId.New(), "WH-JKT", "SKU-1", "QC-A", "B1", Expiry, 5, Gr);

        Assert.True(result.IsSuccess);
        Assert.Equal(StockStatus.Quarantine, result.Value.Status);
        Assert.Equal("QC-A", result.Value.LocationId);
        Assert.Equal(5, result.Value.Quantity);
    }

    [Fact]
    public void Putaway_moves_onhand_to_available_at_rack()
    {
        var stock = OnHand();

        var result = stock.Putaway("RACK-A1");

        Assert.True(result.IsSuccess);
        Assert.Equal(StockStatus.Available, stock.Status);
        Assert.Equal("RACK-A1", stock.LocationId);
    }

    [Fact]
    public void Putaway_from_quarantine_is_illegal()
    {
        var stock = Stock.CreateQuarantine(StockId.New(), "WH-JKT", "SKU-1", "QC-A", "B1", Expiry, 5, Gr).Value;

        var result = stock.Putaway("RACK-A1");

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.InvalidPutaway, result.Error);
        Assert.Equal(StockStatus.Quarantine, stock.Status); // tak berubah
    }

    [Fact]
    public void Putaway_with_blank_destination_fails()
    {
        var stock = OnHand();

        var result = stock.Putaway(" ");

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.MissingLocation, result.Error);
    }

    [Fact]
    public void Allocate_moves_available_to_allocated_with_wave()
    {
        var stock = Available();
        var waveId = Guid.NewGuid();

        var result = stock.Allocate(waveId);

        Assert.True(result.IsSuccess);
        Assert.Equal(StockStatus.Allocated, stock.Status);
        Assert.Equal(waveId, stock.AllocatedToWaveId);
    }

    [Fact]
    public void Allocate_from_onhand_is_illegal()
    {
        var stock = OnHand(); // belum putaway

        var result = stock.Allocate(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.InvalidAllocation, result.Error);
        Assert.Null(stock.AllocatedToWaveId);
    }

    [Fact]
    public void SplitForAllocation_splits_available_into_new_allocated_and_keeps_remainder()
    {
        var stock = Available(); // qty 10 @ RACK-A1
        var waveId = Guid.NewGuid();

        var result = stock.SplitForAllocation(StockId.New(), 3, waveId);

        // porsi teralokasi = aggregate BARU (id sendiri), state Allocated terikat wave, qty = porsi diminta
        Assert.True(result.IsSuccess);
        var allocated = result.Value;
        Assert.NotEqual(stock.Id, allocated.Id);
        Assert.Equal(StockStatus.Allocated, allocated.Status);
        Assert.Equal(waveId, allocated.AllocatedToWaveId);
        Assert.Equal(3, allocated.Quantity);
        Assert.Equal(stock.Sku, allocated.Sku);
        Assert.Equal(stock.LocationId, allocated.LocationId);
        Assert.Equal(stock.Batch, allocated.Batch);
        Assert.Equal(stock.Expiry, allocated.Expiry);
        Assert.Equal(stock.SourceGoodsReceiptId, allocated.SourceGoodsReceiptId);

        // sisa lot tetap Available dengan qty berkurang — konservasi: 7 + 3 = 10 (tak ada unit bocor/ganda)
        Assert.Equal(StockStatus.Available, stock.Status);
        Assert.Equal(7, stock.Quantity);
        Assert.Null(stock.AllocatedToWaveId);
    }

    [Fact]
    public void SplitForAllocation_with_quantity_equal_to_stock_fails()
    {
        var stock = Available(); // qty 10 — porsi PENUH harus pakai Allocate, bukan split

        var result = stock.SplitForAllocation(StockId.New(), 10, Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.InvalidSplitQuantity, result.Error);
        Assert.Equal(StockStatus.Available, stock.Status); // tak berubah
        Assert.Equal(10, stock.Quantity);
    }

    [Fact]
    public void SplitForAllocation_with_quantity_exceeding_stock_fails()
    {
        var stock = Available(); // qty 10

        var result = stock.SplitForAllocation(StockId.New(), 11, Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.InvalidSplitQuantity, result.Error);
        Assert.Equal(10, stock.Quantity); // tak berubah
    }

    [Fact]
    public void SplitForAllocation_from_onhand_is_illegal()
    {
        var stock = OnHand(); // belum putaway → bukan Available

        var result = stock.SplitForAllocation(StockId.New(), 3, Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.InvalidAllocation, result.Error);
        Assert.Equal(10, stock.Quantity); // tak berubah
    }

    [Fact]
    public void Pick_moves_allocated_to_picked_at_staging()
    {
        var stock = Available();
        var waveId = Guid.NewGuid();
        stock.Allocate(waveId);
        var pickingTaskId = Guid.NewGuid();

        var result = stock.Pick(pickingTaskId, "STG-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(StockStatus.Picked, stock.Status);
        Assert.Equal(pickingTaskId, stock.PickingTaskId);
        Assert.Equal("STG-1", stock.LocationId);
        Assert.Equal(waveId, stock.AllocatedToWaveId); // tetap terikat wave
    }

    [Fact]
    public void Pick_from_available_is_illegal()
    {
        var stock = Available(); // belum allocate

        var result = stock.Pick(Guid.NewGuid(), "STG-1");

        Assert.True(result.IsFailure);
        Assert.Equal(StockErrors.InvalidPick, result.Error);
        Assert.Null(stock.PickingTaskId);
    }

    [Fact]
    public void Full_lifecycle_onhand_to_picked_succeeds()
    {
        var stock = OnHand();
        var waveId = Guid.NewGuid();
        var pickingTaskId = Guid.NewGuid();

        Assert.True(stock.Putaway("RACK-A1").IsSuccess);
        Assert.True(stock.Allocate(waveId).IsSuccess);
        Assert.True(stock.Pick(pickingTaskId, "STG-1").IsSuccess);

        Assert.Equal(StockStatus.Picked, stock.Status);
    }
}
