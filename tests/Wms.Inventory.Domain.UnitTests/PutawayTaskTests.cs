using Wms.Inventory.Domain;

namespace Wms.Inventory.Domain.UnitTests;

// What: behavioral fitness untuk aggregate PutawayTask (ADR-0026)
// Why: task dibuat state Assigned dan mereferensikan Stock by id.
public class PutawayTaskTests
{
    [Fact]
    public void Assign_creates_assigned_task_for_stock()
    {
        var stockId = StockId.New();

        var task = PutawayTask.Assign(PutawayTaskId.New(), stockId);

        Assert.Equal(PutawayTaskStatus.Assigned, task.Status);
        Assert.Equal(stockId, task.StockId);
    }
}
