using Microsoft.EntityFrameworkCore;
using Wms.Reporting.Persistence;
using Wms.Reporting.Projections;

namespace Wms.Reporting.Stores;

// What: per-type projection store (ADR-0017) — find-or-create-by-PK + mutate, TANPA SaveChanges
// Why: ADR-0017 amendment (projection-write atomicity) — store hanya mutasi entity TRACKED; consumer sisi
// Inbox yang commit (projection-write + Inbox-mark dalam SATU transaksi via IUnitOfWork). Per-type port
// type-safe — generic IProjectionStore<T> + adapter Cosmos DITOLAK (NoSQL deferred ke spike, ADR-0017).
public interface IStockOnHandViewStore
{
    Task ApplyReceivedAsync(
        string warehouseId, string sku, string? batch, int qty, CancellationToken cancellationToken = default);

    Task ApplyRemovedAsync(
        string warehouseId, string sku, string? batch, int qty, CancellationToken cancellationToken = default);
}

// What: adapter EF atas ReportingDbContext (= DbContext ambient yang sama dgn IUnitOfWork → commit atomic).
// How: find-or-create-by-PK (warehouse, sku, batch); batch null → "" (PK non-null). NO SaveChanges.
internal sealed class StockOnHandViewStore(ReportingDbContext db) : IStockOnHandViewStore
{
    public async Task ApplyReceivedAsync(
        string warehouseId, string sku, string? batch, int qty, CancellationToken cancellationToken = default)
        => (await FindOrCreateAsync(warehouseId, sku, batch, cancellationToken)).Receive(qty);

    public async Task ApplyRemovedAsync(
        string warehouseId, string sku, string? batch, int qty, CancellationToken cancellationToken = default)
        => (await FindOrCreateAsync(warehouseId, sku, batch, cancellationToken)).Remove(qty);

    private async Task<StockOnHandView> FindOrCreateAsync(
        string warehouseId, string sku, string? batch, CancellationToken cancellationToken)
    {
        var batchKey = batch ?? string.Empty;
        var view = await db.StockOnHandViews.FindAsync([warehouseId, sku, batchKey], cancellationToken);
        if (view is null)
        {
            view = new StockOnHandView(warehouseId, sku, batchKey);
            db.StockOnHandViews.Add(view);
        }

        return view;
    }
}
