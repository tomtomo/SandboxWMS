using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Wms.Reporting.Persistence;

namespace Wms.Reporting.Endpoints;

// What: query REST endpoints Reporting (CQRS read-side; ADR-0004/0006/0017)
// Why: dashboard membaca projection. READ-SIDE BYPASS (ADR-0004): query baca LANGSUNG projection → DTO,
// tanpa aggregate/repo/MediatR — inti CQRS read. Reporting modul COLLAPSED (bukan layer Api terpisah) →
// FF#8 (Api⊅DbContext) tak berlaku: baca ReportingDbContext di sini benar. AsNoTracking (read murni).
// AuthZ deferred (ADR-0012) → penanda TODO-AUTH; enforcement + permission catalog di 07a.
public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Reporting.ViewStockOnHand
        app.MapGet("/reports/stock-on-hand", async (
            ReportingDbContext db, string? sku, string? warehouseId, CancellationToken cancellationToken) =>
        {
            var query = db.StockOnHandViews.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(sku))
                query = query.Where(view => view.Sku == sku);
            if (!string.IsNullOrWhiteSpace(warehouseId))
                query = query.Where(view => view.WarehouseId == warehouseId);

            var rows = await query
                .OrderBy(view => view.WarehouseId).ThenBy(view => view.Sku).ThenBy(view => view.Batch)
                .Select(view => new StockOnHandRow(view.WarehouseId, view.Sku, view.Batch, view.QtyOnHand))
                .ToListAsync(cancellationToken);
            return Results.Ok(rows);
        });

        // TODO-AUTH: Reporting.ViewReceivingSummary
        app.MapGet("/reports/receiving-summary", async (ReportingDbContext db, CancellationToken cancellationToken) =>
        {
            // discrepancy rate dihitung client-side (hindari divide-by-zero & risiko translasi SQL ternary)
            var summaries = await db.ReceivingSummaries.AsNoTracking()
                .OrderByDescending(summary => summary.Day).ThenBy(summary => summary.SupplierId)
                .ToListAsync(cancellationToken);

            var rows = summaries.Select(summary => new ReceivingSummaryRow(
                summary.SupplierId, summary.Day, summary.GrCount, summary.ReceivedQty, summary.RejectedQty,
                summary.ReceivedQty + summary.RejectedQty == 0
                    ? 0
                    : (double)summary.RejectedQty / (summary.ReceivedQty + summary.RejectedQty)));
            return Results.Ok(rows);
        });

        // TODO-AUTH: Reporting.ViewDispatchSummary
        app.MapGet("/reports/dispatch-summary", async (ReportingDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await db.DispatchSummaries.AsNoTracking()
                .OrderByDescending(summary => summary.Day)
                .Select(summary => new DispatchSummaryRow(summary.Day, summary.WaveCount, summary.TotalVolume))
                .ToListAsync(cancellationToken);
            return Results.Ok(rows);
        });

        // TODO-AUTH: Reporting.ViewOperatorActivity
        app.MapGet("/reports/operator-activity", async (ReportingDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await db.OperatorActivities.AsNoTracking()
                .OrderByDescending(activity => activity.Day).ThenBy(activity => activity.OperatorId)
                .Select(activity => new OperatorActivityRow(
                    activity.OperatorId, activity.Day, activity.PutawayCount, activity.PickCount))
                .ToListAsync(cancellationToken);
            return Results.Ok(rows);
        });

        return app;
    }
}

// What: read DTO (CQRS read-side) — bentuk respons query, decoupled dari projection entity
public sealed record StockOnHandRow(string WarehouseId, string Sku, string Batch, int QtyOnHand);

public sealed record ReceivingSummaryRow(
    string SupplierId, DateOnly Day, int GrCount, int ReceivedQty, int RejectedQty, double DiscrepancyRate);

public sealed record DispatchSummaryRow(DateOnly Day, int WaveCount, int TotalVolume);

public sealed record OperatorActivityRow(string OperatorId, DateOnly Day, int PutawayCount, int PickCount);
