using Wms.Outbound.Domain;

namespace Wms.Outbound.Domain.UnitTests;

// What: behavioral fitness untuk aggregate Wave — lifecycle Active→Ready→Dispatched (overview §C, ADR-0026)
// Why: Wave grouping order untuk picking/dispatch bersama. Active saat dibuat (emit WaveReleased di handler);
// Ready saat SEMUA PickingTask Completed (aturan agregasi di domain); Dispatched saat SPV dispatch (raise
// ShipmentDispatched). Transisi ilegal ditolak via Result (no-throw, FF#7).
public class WaveTests
{
    private static Wave Active(params Guid[] orderIds) =>
        Wave.Activate(WaveId.New(), orderIds.Length == 0 ? [Guid.NewGuid()] : orderIds).Value;

    [Fact]
    public void Activate_with_orders_succeeds_as_active()
    {
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();

        var result = Wave.Activate(WaveId.New(), [orderA, orderB]);

        Assert.True(result.IsSuccess);
        Assert.Equal(WaveStatus.Active, result.Value.Status);
        Assert.Equal(new[] { orderA, orderB }, result.Value.OrderIds);
        Assert.Empty(result.Value.PickingTaskIds);
    }

    [Fact]
    public void Activate_with_no_orders_fails()
    {
        var result = Wave.Activate(WaveId.New(), []);

        Assert.True(result.IsFailure);
        Assert.Equal(WaveErrors.NoOrders, result.Error);
    }

    [Fact]
    public void AttachPickingTasks_adds_tasks_to_active_wave()
    {
        var wave = Active();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();

        var result = wave.AttachPickingTasks([t1, t2]);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { t1, t2 }, wave.PickingTaskIds);
    }

    [Fact]
    public void MarkReady_when_all_picking_tasks_completed_succeeds()
    {
        var wave = Active();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        wave.AttachPickingTasks([t1, t2]);

        var result = wave.MarkReady([t1, t2]);

        Assert.True(result.IsSuccess);
        Assert.Equal(WaveStatus.Ready, wave.Status);
    }

    [Fact]
    public void MarkReady_when_not_all_completed_is_illegal()
    {
        var wave = Active();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        wave.AttachPickingTasks([t1, t2]);

        var result = wave.MarkReady([t1]); // t2 belum completed

        Assert.True(result.IsFailure);
        Assert.Equal(WaveErrors.NotAllPicked, result.Error);
        Assert.Equal(WaveStatus.Active, wave.Status); // tak berubah
    }

    [Fact]
    public void MarkReady_with_no_picking_tasks_is_illegal()
    {
        var wave = Active(); // belum ada picking task

        var result = wave.MarkReady([]);

        Assert.True(result.IsFailure);
        Assert.Equal(WaveErrors.NotAllPicked, result.Error);
        Assert.Equal(WaveStatus.Active, wave.Status);
    }

    [Fact]
    public void AttachPickingTasks_when_not_active_is_illegal()
    {
        var wave = Active();
        var t1 = Guid.NewGuid();
        wave.AttachPickingTasks([t1]);
        wave.MarkReady([t1]); // → Ready

        var result = wave.AttachPickingTasks([Guid.NewGuid()]);

        Assert.True(result.IsFailure);
        Assert.Equal(WaveErrors.NotActive, result.Error);
    }

    [Fact]
    public void Dispatch_moves_ready_to_dispatched_and_raises_event()
    {
        var wave = Active();
        var t1 = Guid.NewGuid();
        wave.AttachPickingTasks([t1]);
        wave.MarkReady([t1]);

        var result = wave.Dispatch();

        Assert.True(result.IsSuccess);
        Assert.Equal(WaveStatus.Dispatched, wave.Status);
        var dispatched = Assert.Single(wave.DomainEvents.OfType<ShipmentDispatched>());
        Assert.Equal(wave.Id, dispatched.WaveId);
    }

    [Fact]
    public void Dispatch_from_active_is_illegal()
    {
        var wave = Active(); // belum Ready

        var result = wave.Dispatch();

        Assert.True(result.IsFailure);
        Assert.Equal(WaveErrors.InvalidDispatch, result.Error);
        Assert.Equal(WaveStatus.Active, wave.Status);
        Assert.Empty(wave.DomainEvents.OfType<ShipmentDispatched>()); // no event saat guard gagal
    }
}
