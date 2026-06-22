using Microsoft.EntityFrameworkCore;
using Wms.Reporting.Persistence;
using Wms.Reporting.Projections;

namespace Wms.Reporting.Stores;

// What: per-type projection store DispatchSummary (ADR-0017) — find-or-create-by-PK + mutate, NO SaveChanges
// Why: lihat IStockOnHandViewStore — store mutasi tracked, consumer Inbox commit atomic.
public interface IDispatchSummaryStore
{
    Task ApplyDispatchedAsync(DateOnly day, int volume, CancellationToken cancellationToken = default);
}

internal sealed class DispatchSummaryStore(ReportingDbContext db) : IDispatchSummaryStore
{
    public async Task ApplyDispatchedAsync(DateOnly day, int volume, CancellationToken cancellationToken = default)
    {
        var summary = await db.DispatchSummaries.FindAsync([day], cancellationToken);
        if (summary is null)
        {
            summary = new DispatchSummary(day);
            db.DispatchSummaries.Add(summary);
        }

        summary.AddDispatch(volume);
    }
}
