using Wms.Outbound.Domain;

namespace Wms.Outbound.Domain.UnitTests;

// What: behavioral fitness untuk aggregate PickingTask — Assigned→Completed (overview §C4/C5, ADR-0026/0028)
// Why: task dibuat Assigned saat StockAllocated dikonsumsi (mereferensikan Stock by id + lokasi rak source);
// Complete (operator scan + staging) menyimpan actualQty (=qty di scope) + stagingLocation lalu RAISE
// PickingCompleted (memicu Stock Allocated→Picked di Inventory, ADR-0028). Transisi ilegal ditolak via Result.
public class PickingTaskTests
{
    private static readonly Guid Wave = Guid.NewGuid();
    private static readonly Guid Stock = Guid.NewGuid();

    private static PickingTask Assigned() =>
        PickingTask.Assign(PickingTaskId.New(), Wave, Stock, "RACK-A1", "SKU-1", "B1", 10, "op-1");

    [Fact]
    public void Assign_creates_assigned_task_with_allocation_detail()
    {
        var stockId = Guid.NewGuid();

        var task = PickingTask.Assign(PickingTaskId.New(), Wave, stockId, "RACK-A1", "SKU-1", "B1", 10, "op-1");

        Assert.Equal(PickingTaskStatus.Assigned, task.Status);
        Assert.Equal(Wave, task.WaveId);
        Assert.Equal(stockId, task.StockId);
        Assert.Equal("RACK-A1", task.SourceLocationId);
        Assert.Equal("SKU-1", task.Sku);
        Assert.Equal("B1", task.Batch);
        Assert.Equal(10, task.Qty);
        Assert.Equal("op-1", task.AssignedTo);
        Assert.Null(task.ActualQty);
        Assert.Null(task.StagingLocationId);
    }

    [Fact]
    public void Complete_moves_assigned_to_completed_with_actual_and_staging()
    {
        var task = Assigned();

        var result = task.Complete("STG-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(PickingTaskStatus.Completed, task.Status);
        Assert.Equal(10, task.ActualQty);          // scope: actualQty = qty (picking discrepancy out-of-scope)
        Assert.Equal("STG-1", task.StagingLocationId);
    }

    [Fact]
    public void Complete_raises_picking_completed_event()
    {
        var task = Assigned();

        task.Complete("STG-1");

        var completed = Assert.Single(task.DomainEvents.OfType<PickingCompleted>());
        Assert.Equal(Wave, completed.WaveId);
        Assert.Equal(task.Id, completed.PickingTaskId);
        Assert.Equal(Stock, completed.StockId);
        Assert.Equal("SKU-1", completed.Sku);
        Assert.Equal("B1", completed.Batch);
        Assert.Equal(10, completed.Qty);
        Assert.Equal("STG-1", completed.StagingLocationId);
    }

    [Fact]
    public void Complete_with_blank_staging_fails()
    {
        var task = Assigned();

        var result = task.Complete(" ");

        Assert.True(result.IsFailure);
        Assert.Equal(PickingTaskErrors.MissingStagingLocation, result.Error);
        Assert.Equal(PickingTaskStatus.Assigned, task.Status); // tak berubah
        Assert.Empty(task.DomainEvents.OfType<PickingCompleted>());
    }

    [Fact]
    public void Complete_when_already_completed_is_illegal()
    {
        var task = Assigned();
        task.Complete("STG-1");

        var result = task.Complete("STG-2");

        Assert.True(result.IsFailure);
        Assert.Equal(PickingTaskErrors.InvalidCompletion, result.Error);
        Assert.Equal("STG-1", task.StagingLocationId); // tak berubah
    }
}
