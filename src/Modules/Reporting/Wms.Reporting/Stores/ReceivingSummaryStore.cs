using Microsoft.EntityFrameworkCore;
using Wms.Reporting.Persistence;
using Wms.Reporting.Projections;

namespace Wms.Reporting.Stores;

// What: per-type projection store ReceivingSummary (ADR-0017) — find-or-create-by-PK + mutate, NO SaveChanges
// Why: lihat IStockOnHandViewStore — store mutasi tracked, consumer Inbox commit atomic.
public interface IReceivingSummaryStore
{
    Task ApplyConfirmedAsync(
        string? supplierId, DateOnly day, int receivedQty, int rejectedQty, CancellationToken cancellationToken = default);
}

internal sealed class ReceivingSummaryStore(ReportingDbContext db) : IReceivingSummaryStore
{
    public async Task ApplyConfirmedAsync(
        string? supplierId, DateOnly day, int receivedQty, int rejectedQty, CancellationToken cancellationToken = default)
    {
        var supplierKey = supplierId ?? string.Empty;
        var summary = await db.ReceivingSummaries.FindAsync([supplierKey, day], cancellationToken);
        if (summary is null)
        {
            summary = new ReceivingSummary(supplierKey, day);
            db.ReceivingSummaries.Add(summary);
        }

        summary.AddReceipt(receivedQty, rejectedQty);
    }
}
