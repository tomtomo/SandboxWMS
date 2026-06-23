using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.Reporting.Persistence;

namespace Wms.Reporting.Endpoints;

// What: query REST endpoints Reporting (CQRS read-side; ADR-0004/0006/0017)
// Why: dashboard membaca projection. READ-SIDE BYPASS (ADR-0004): query baca LANGSUNG projection → DTO,
// tanpa aggregate/repo/MediatR — inti CQRS read. Reporting modul COLLAPSED (bukan layer Api terpisah) →
// FF#8 (Api⊅DbContext) tak berlaku: baca ReportingDbContext di sini benar. AsNoTracking (read murni).
// Paginated (PagedResult, Skip/Take + CountAsync) — cegah unbounded result set (Nygard, Release It!).
// AuthZ deferred (ADR-0012) → penanda TODO-AUTH; enforcement + permission catalog di 07a.
public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Reporting.ViewStockOnHand
        app.MapGet("/reports/stock-on-hand", async (
            ReportingDbContext db, string? sku, string? warehouseId,
            int? page, int? pageSize, CancellationToken cancellationToken) =>
        {
            var (safePage, safeSize) = PageRequest.From(page, pageSize);

            var query = db.StockOnHandViews.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(sku))
                query = query.Where(view => view.Sku == sku);
            if (!string.IsNullOrWhiteSpace(warehouseId))
                query = query.Where(view => view.WarehouseId == warehouseId);

            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderBy(view => view.WarehouseId).ThenBy(view => view.Sku).ThenBy(view => view.Batch)
                .Skip((safePage - 1) * safeSize).Take(safeSize)
                .Select(view => new StockOnHandRow(view.WarehouseId, view.Sku, view.Batch, view.QtyOnHand))
                .ToListAsync(cancellationToken);
            return Results.Ok(new PagedResult<StockOnHandRow>(rows, safePage, safeSize, total));
        });

        // TODO-AUTH: Reporting.ViewReceivingSummary
        app.MapGet("/reports/receiving-summary", async (
            ReportingDbContext db, int? page, int? pageSize, CancellationToken cancellationToken) =>
        {
            var (safePage, safeSize) = PageRequest.From(page, pageSize);

            var query = db.ReceivingSummaries.AsNoTracking();
            var total = await query.CountAsync(cancellationToken);

            // discrepancy rate dihitung client-side (hindari divide-by-zero & risiko translasi SQL ternary)
            var summaries = await query
                .OrderByDescending(summary => summary.Day).ThenBy(summary => summary.SupplierId)
                .Skip((safePage - 1) * safeSize).Take(safeSize)
                .ToListAsync(cancellationToken);

            var rows = summaries.Select(summary => new ReceivingSummaryRow(
                summary.SupplierId, summary.Day, summary.GrCount, summary.ReceivedQty, summary.RejectedQty,
                summary.ReceivedQty + summary.RejectedQty == 0
                    ? 0
                    : (double)summary.RejectedQty / (summary.ReceivedQty + summary.RejectedQty))).ToList();
            return Results.Ok(new PagedResult<ReceivingSummaryRow>(rows, safePage, safeSize, total));
        });

        // TODO-AUTH: Reporting.ViewDispatchSummary
        app.MapGet("/reports/dispatch-summary", async (
            ReportingDbContext db, int? page, int? pageSize, CancellationToken cancellationToken) =>
        {
            var (safePage, safeSize) = PageRequest.From(page, pageSize);

            var query = db.DispatchSummaries.AsNoTracking();
            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderByDescending(summary => summary.Day)
                .Skip((safePage - 1) * safeSize).Take(safeSize)
                .Select(summary => new DispatchSummaryRow(summary.Day, summary.WaveCount, summary.TotalVolume))
                .ToListAsync(cancellationToken);
            return Results.Ok(new PagedResult<DispatchSummaryRow>(rows, safePage, safeSize, total));
        });

        // TODO-AUTH: Reporting.ViewOperatorActivity
        app.MapGet("/reports/operator-activity", async (
            ReportingDbContext db, int? page, int? pageSize, CancellationToken cancellationToken) =>
        {
            var (safePage, safeSize) = PageRequest.From(page, pageSize);

            var query = db.OperatorActivities.AsNoTracking();
            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderByDescending(activity => activity.Day).ThenBy(activity => activity.OperatorId)
                .Skip((safePage - 1) * safeSize).Take(safeSize)
                .Select(activity => new OperatorActivityRow(
                    activity.OperatorId, activity.Day, activity.PutawayCount, activity.PickCount))
                .ToListAsync(cancellationToken);
            return Results.Ok(new PagedResult<OperatorActivityRow>(rows, safePage, safeSize, total));
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
