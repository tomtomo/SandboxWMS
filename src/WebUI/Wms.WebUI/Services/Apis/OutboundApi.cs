using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Outbound (ADR-0006). Backend write-only: 4 POST functional, 5 GET = GAP
// (webui-api-gaps.md). Paths BARE. Create return { id }. Enum REQUEST tak ada di body; status filter
// string-name. Payload stripped: no WarehouseId/Uom/ActualQty.
public sealed class OutboundApi(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    // ---- Orders ----
    public Task<ApiResult<PagedResultDto<OrderSummaryDto>>> ListOrdersAsync(
        int page, int pageSize, OutboundOrderStatus? status, CancellationToken cancellationToken = default)
    {
        var q = $"/outbound-orders?page={page}&pageSize={pageSize}";
        if (status.HasValue) q += $"&status={status.Value}";
        return GetPagedAsync<OrderSummaryDto>(q, "🚧 Outbound query API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);
    }

    public Task<ApiResult<OrderDetailDto>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
        => GetOneAsync<OrderDetailDto>($"/outbound-orders/{id}", "🚧 Order detail API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);

    public async Task<ApiResult<Guid>> CreateOrderAsync(CreateOrderForm form, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            form.CustomerId,
            form.ShipTo,
            Lines = form.Lines.Select(l => new { l.Sku, l.Qty }).ToList(),
        };
        return await PostCreateAsync("/outbound-orders", payload, cancellationToken);
    }

    // ---- Waves ----
    public Task<ApiResult<PagedResultDto<WaveSummaryDto>>> ListWavesAsync(
        int page, int pageSize, WaveStatus? status, CancellationToken cancellationToken = default)
    {
        var q = $"/waves?page={page}&pageSize={pageSize}";
        if (status.HasValue) q += $"&status={status.Value}";
        return GetPagedAsync<WaveSummaryDto>(q, "🚧 Outbound query API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);
    }

    public Task<ApiResult<WaveDetailDto>> GetWaveAsync(Guid id, CancellationToken cancellationToken = default)
        => GetOneAsync<WaveDetailDto>($"/waves/{id}", "🚧 Wave detail API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);

    public Task<ApiResult<Guid>> CreateWaveAsync(IReadOnlyList<Guid> orderIds, CancellationToken cancellationToken = default)
        => PostCreateAsync("/waves", new { OrderIds = orderIds }, cancellationToken);

    public Task<ApiResult> DispatchWaveAsync(Guid id, CancellationToken cancellationToken = default)
        => PostEmptyAsync($"/waves/{id}/dispatch", cancellationToken);

    // ---- PickingTasks ----
    public Task<ApiResult<PagedResultDto<PickingTaskDto>>> ListPickingTasksAsync(
        int page, int pageSize, string? assignedTo, PickingTaskStatus? status, Guid? waveId,
        CancellationToken cancellationToken = default)
    {
        var q = $"/picking-tasks?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(assignedTo)) q += $"&assignedTo={Uri.EscapeDataString(assignedTo)}";
        if (status.HasValue) q += $"&status={status.Value}";
        if (waveId.HasValue) q += $"&waveId={waveId.Value}";
        return GetPagedAsync<PickingTaskDto>(q, "🚧 Outbound query API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);
    }

    public Task<ApiResult> CompletePickingAsync(Guid taskId, CompletePickingForm form, CancellationToken cancellationToken = default)
        => PostJsonAsync($"/picking-tasks/{taskId}/complete", new { form.StagingLocationId }, cancellationToken);
}
