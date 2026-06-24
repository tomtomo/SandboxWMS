using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Inventory (ADR-0006). Backend Inventory command/event-heavy: HANYA
// POST /putaway-tasks/{id}/complete yang ada. List Stocks/PutawayTasks + adjust = GAP (webui-api-gaps.md):
// di-wire ke kontrak DITUJU; 404 → ApiResult.Fail (graceful-degrade). Paths BARE (tanpa /inventory prefix).
// status filter dikirim string-name (intended contract). 204 no-body = success.
public sealed class InventoryApi(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    public Task<ApiResult<PagedResultDto<StockDto>>> ListStocksAsync(
        int page, int pageSize, string? warehouseId, string? sku, StockStatus? status,
        CancellationToken cancellationToken = default)
    {
        var q = $"/stocks?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(warehouseId)) q += $"&warehouseId={Uri.EscapeDataString(warehouseId)}";
        if (!string.IsNullOrWhiteSpace(sku)) q += $"&sku={Uri.EscapeDataString(sku)}";
        if (status.HasValue) q += $"&status={status.Value}"; // string enum-name (intended contract)
        return GetPagedAsync<StockDto>(q, "🚧 Inventory query API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);
    }

    public Task<ApiResult<PagedResultDto<PutawayTaskDto>>> ListPutawayTasksAsync(
        int page, int pageSize, string? assignedTo, PutawayTaskStatus? status,
        CancellationToken cancellationToken = default)
    {
        var q = $"/putaway-tasks?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(assignedTo)) q += $"&assignedTo={Uri.EscapeDataString(assignedTo)}";
        if (status.HasValue) q += $"&status={status.Value}"; // string enum-name (intended contract)
        return GetPagedAsync<PutawayTaskDto>(q, "🚧 Inventory query API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);
    }

    // FUNCTIONAL: body { ActualDestinationId } (STRING) → 204
    public Task<ApiResult> CompletePutawayAsync(Guid taskId, string actualDestinationId, CancellationToken cancellationToken = default)
        => PostJsonAsync($"/putaway-tasks/{taskId}/complete", new { ActualDestinationId = actualDestinationId }, cancellationToken);

    // GAP: body { NewQty } (absolute set) → 204
    public Task<ApiResult> AdjustStockAsync(Guid stockId, int newQty, CancellationToken cancellationToken = default)
        => PostJsonAsync($"/stocks/{stockId}/adjust", new { NewQty = newQty }, cancellationToken);
}
