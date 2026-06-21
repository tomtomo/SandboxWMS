using Wms.Inventory.Domain;

namespace Wms.Inventory.Domain.UnitTests;

// What: behavioral fitness untuk aggregate PutawayTask (ADR-0026, Phase 03b)
// Why: task dibuat state Assigned (mereferensikan Stock by id + lokasi source/suggested) lalu
// Complete (Assigned→Completed) menyimpan actualDestination. Transisi ilegal ditolak via Result.
public class PutawayTaskTests
{
    private static PutawayTask Assigned() =>
        PutawayTask.Assign(PutawayTaskId.New(), StockId.New(), "REC-01", "RACK-A1", "op-1");

    [Fact]
    public void Assign_creates_assigned_task_with_locations()
    {
        var stockId = StockId.New();

        var task = PutawayTask.Assign(PutawayTaskId.New(), stockId, "REC-01", "RACK-A1", "op-1");

        Assert.Equal(PutawayTaskStatus.Assigned, task.Status);
        Assert.Equal(stockId, task.StockId);
        Assert.Equal("REC-01", task.SourceLocationId);
        Assert.Equal("RACK-A1", task.SuggestedDestinationId);
        Assert.Equal("op-1", task.AssignedTo);
        Assert.Null(task.ActualDestinationId);
    }

    [Fact]
    public void Complete_moves_assigned_to_completed_with_actual_destination()
    {
        var task = Assigned();

        var result = task.Complete("RACK-B12");

        Assert.True(result.IsSuccess);
        Assert.Equal(PutawayTaskStatus.Completed, task.Status);
        Assert.Equal("RACK-B12", task.ActualDestinationId);
    }

    [Fact]
    public void Complete_with_blank_destination_fails()
    {
        var task = Assigned();

        var result = task.Complete(" ");

        Assert.True(result.IsFailure);
        Assert.Equal(PutawayTaskErrors.MissingDestination, result.Error);
        Assert.Equal(PutawayTaskStatus.Assigned, task.Status);
    }

    [Fact]
    public void Complete_when_already_completed_is_illegal()
    {
        var task = Assigned();
        task.Complete("RACK-B12");

        var result = task.Complete("RACK-C3");

        Assert.True(result.IsFailure);
        Assert.Equal(PutawayTaskErrors.InvalidCompletion, result.Error);
        Assert.Equal("RACK-B12", task.ActualDestinationId); // tak berubah
    }
}
