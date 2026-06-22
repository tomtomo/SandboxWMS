using Microsoft.EntityFrameworkCore;
using Wms.Reporting.Persistence;
using Wms.Reporting.Projections;

namespace Wms.Reporting.Stores;

// What: per-type projection store OperatorActivity (ADR-0017) — find-or-create-by-PK + mutate, NO SaveChanges
// Why: lihat IStockOnHandViewStore — store mutasi tracked, consumer Inbox commit atomic.
public interface IOperatorActivityStore
{
    Task ApplyPutawayAsync(string? operatorId, DateOnly day, CancellationToken cancellationToken = default);

    Task ApplyPickAsync(string? operatorId, DateOnly day, CancellationToken cancellationToken = default);
}

internal sealed class OperatorActivityStore(ReportingDbContext db) : IOperatorActivityStore
{
    public async Task ApplyPutawayAsync(string? operatorId, DateOnly day, CancellationToken cancellationToken = default)
        => (await FindOrCreateAsync(operatorId, day, cancellationToken)).RecordPutaway();

    public async Task ApplyPickAsync(string? operatorId, DateOnly day, CancellationToken cancellationToken = default)
        => (await FindOrCreateAsync(operatorId, day, cancellationToken)).RecordPick();

    private async Task<OperatorActivity> FindOrCreateAsync(
        string? operatorId, DateOnly day, CancellationToken cancellationToken)
    {
        var operatorKey = operatorId ?? string.Empty;
        var activity = await db.OperatorActivities.FindAsync([operatorKey, day], cancellationToken);
        if (activity is null)
        {
            activity = new OperatorActivity(operatorKey, day);
            db.OperatorActivities.Add(activity);
        }

        return activity;
    }
}
