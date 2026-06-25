using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.Reporting.Directory;
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
            ReportingDbContext db, IUserDirectory userDirectory,
            int? page, int? pageSize, CancellationToken cancellationToken) =>
        {
            var (safePage, safeSize) = PageRequest.From(page, pageSize);

            var query = db.OperatorActivities.AsNoTracking();
            var total = await query.CountAsync(cancellationToken);
            var activities = await query
                .OrderByDescending(activity => activity.Day).ThenBy(activity => activity.OperatorId)
                .Skip((safePage - 1) * safeSize).Take(safeSize)
                .ToListAsync(cancellationToken);

            // What: enrichment-at-read (ACL) — OperatorId → username via Auth read-API (ADR-0011)
            // Why: projection key-by id (stabil utk rebuild; username Auth-owned & mutable) → nama di-resolve
            // saat query, BUKAN di-denormalize ke projection. Why dedupe: hindari N+1 — satu gRPC call per
            // operator unik di halaman, bukan per-row.
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var operatorId in activities.Select(activity => activity.OperatorId).Distinct())
                names[operatorId] = await ResolveOperatorNameAsync(userDirectory, operatorId, cancellationToken);

            var rows = activities
                .Select(activity => new OperatorActivityRow(
                    activity.OperatorId, names[activity.OperatorId],
                    activity.Day, activity.PutawayCount, activity.PickCount))
                .ToList();
            return Results.Ok(new PagedResult<OperatorActivityRow>(rows, safePage, safeSize, total));
        });

        return app;
    }

    // id sistem/anonim (authZ deferred → 07a; ICurrentUser.UserId = "SYSTEM"/"anonymous" sebelum atribusi nyata)
    private static readonly HashSet<string> SystemOperatorIds =
        new(StringComparer.OrdinalIgnoreCase) { "SYSTEM", "anonymous" };

    // resolve username dgn fallback berlapis: label sistem → username Auth → id mentah (user tak ditemukan)
    private static async Task<string> ResolveOperatorNameAsync(
        IUserDirectory userDirectory, string operatorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operatorId) || SystemOperatorIds.Contains(operatorId))
            return "SYSTEM";
        var username = await userDirectory.GetUsernameAsync(operatorId, cancellationToken);
        return string.IsNullOrWhiteSpace(username) ? operatorId : username;
    }
}

// What: read DTO (CQRS read-side) — bentuk respons query, decoupled dari projection entity
public sealed record StockOnHandRow(string WarehouseId, string Sku, string Batch, int QtyOnHand);

public sealed record ReceivingSummaryRow(
    string SupplierId, DateOnly Day, int GrCount, int ReceivedQty, int RejectedQty, double DiscrepancyRate);

public sealed record DispatchSummaryRow(DateOnly Day, int WaveCount, int TotalVolume);

public sealed record OperatorActivityRow(
    string OperatorId, string OperatorName, DateOnly Day, int PutawayCount, int PickCount);
