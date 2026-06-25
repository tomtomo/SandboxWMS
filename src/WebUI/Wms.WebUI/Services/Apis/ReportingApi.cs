using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Reporting (ADR-0006/0017) — stock-on-hand + receiving summary via gateway.
public sealed class ReportingApi(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    // ---- paginated (M5 dashboard; functional) ----
    public Task<ApiResult<PagedResultDto<StockOnHandRow>>> ListStockOnHandAsync(
        int page, int pageSize, string? sku, string? warehouseId, CancellationToken cancellationToken = default)
    {
        var q = $"/reports/stock-on-hand?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(sku)) q += $"&sku={Uri.EscapeDataString(sku)}";
        if (!string.IsNullOrWhiteSpace(warehouseId)) q += $"&warehouseId={Uri.EscapeDataString(warehouseId)}";
        return GetPagedAsync<StockOnHandRow>(q, gapMessage: null, cancellationToken);
    }

    public Task<ApiResult<PagedResultDto<ReceivingSummaryRow>>> ListReceivingSummaryAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
        => GetPagedAsync<ReceivingSummaryRow>($"/reports/receiving-summary?page={page}&pageSize={pageSize}", gapMessage: null, cancellationToken);

    public Task<ApiResult<PagedResultDto<DispatchSummaryRow>>> ListDispatchSummaryAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
        => GetPagedAsync<DispatchSummaryRow>($"/reports/dispatch-summary?page={page}&pageSize={pageSize}", gapMessage: null, cancellationToken);

    public Task<ApiResult<PagedResultDto<OperatorActivityRow>>> ListOperatorActivityAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
        => GetPagedAsync<OperatorActivityRow>($"/reports/operator-activity?page={page}&pageSize={pageSize}", gapMessage: null, cancellationToken);
}

public sealed record StockOnHandRow(string WarehouseId, string Sku, string Batch, int QtyOnHand);

public sealed record ReceivingSummaryRow(
    string SupplierId, DateOnly Day, int GrCount, int ReceivedQty, int RejectedQty, double DiscrepancyRate);

public sealed record DispatchSummaryRow(DateOnly Day, int WaveCount, int TotalVolume);

// OperatorName = username ter-resolve server-side (Auth read-API); fallback ke id/"SYSTEM" bila tak ditemukan.
public sealed record OperatorActivityRow(
    string OperatorId, string OperatorName, DateOnly Day, int PutawayCount, int PickCount);
