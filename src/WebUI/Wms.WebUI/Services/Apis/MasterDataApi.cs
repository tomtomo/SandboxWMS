using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST MasterData (ADR-0006) — Products/Warehouses/Locations via gateway.
// Why: backend current command-heavy. CREATE + DEACTIVATE + GET-by-id ada; LIST + UPDATE = GAP
// (webui-api-gaps.md). List* di-wire ke kontrak list yang DITUJU; 404 → ApiResult.Fail (gap) supaya
// halaman graceful-degrade. Location Type dikirim STRING enum-name (backend Enum.TryParse). Deactivate
// = POST /{id}/deactivate (bukan DELETE legacy).
public sealed class MasterDataApi(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    // ---- Products ----
    public Task<ApiResult<PagedResultDto<ProductDto>>> ListProductsAsync(
        int page, int pageSize, bool? isActive, string? search, CancellationToken cancellationToken = default)
    {
        var q = $"/products?page={page}&pageSize={pageSize}";
        if (isActive.HasValue) q += $"&isActive={(isActive.Value ? "true" : "false")}";
        if (!string.IsNullOrWhiteSpace(search)) q += $"&search={Uri.EscapeDataString(search)}";
        return GetPagedAsync<ProductDto>(q, "🚧 List API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);
    }

    public Task<ApiResult> CreateProductAsync(ProductForm form, CancellationToken cancellationToken = default)
        => PostJsonAsync("/products", new
        {
            form.Sku, form.Name, form.Uom, form.BatchTrackingRequired,
            form.ExpiryTrackingRequired, form.QcRequiredOnReceipt, form.ShelfLifeDays,
        }, cancellationToken);

    public Task<ApiResult> DeactivateProductAsync(string sku, CancellationToken cancellationToken = default)
        => PostEmptyAsync($"/products/{Uri.EscapeDataString(sku)}/deactivate", cancellationToken);

    // ---- Warehouses ----
    public Task<ApiResult<PagedResultDto<WarehouseDto>>> ListWarehousesAsync(
        int page, int pageSize, bool? isActive, CancellationToken cancellationToken = default)
    {
        var q = $"/warehouses?page={page}&pageSize={pageSize}";
        if (isActive.HasValue) q += $"&isActive={(isActive.Value ? "true" : "false")}";
        return GetPagedAsync<WarehouseDto>(q, "🚧 List API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);
    }

    public Task<ApiResult> CreateWarehouseAsync(WarehouseForm form, CancellationToken cancellationToken = default)
        => PostJsonAsync("/warehouses", new { form.Name, form.Address }, cancellationToken);

    public Task<ApiResult> DeactivateWarehouseAsync(Guid id, CancellationToken cancellationToken = default)
        => PostEmptyAsync($"/warehouses/{id}/deactivate", cancellationToken);

    // ---- Locations ----
    public Task<ApiResult<PagedResultDto<LocationDto>>> ListLocationsAsync(
        int page, int pageSize, Guid? warehouseId, LocationType? type, bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var q = $"/locations?page={page}&pageSize={pageSize}";
        if (warehouseId.HasValue) q += $"&warehouseId={warehouseId.Value}";
        if (type.HasValue) q += $"&type={type.Value}"; // enum-name string
        if (isActive.HasValue) q += $"&isActive={(isActive.Value ? "true" : "false")}";
        return GetPagedAsync<LocationDto>(q, "🚧 List API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);
    }

    public Task<ApiResult> CreateLocationAsync(LocationForm form, CancellationToken cancellationToken = default)
        => PostJsonAsync("/locations", new
        {
            WarehouseId = form.WarehouseId!.Value,
            Type = form.Type!.Value.ToString(), // STRING enum-name (backend Enum.TryParse)
            form.Code,
        }, cancellationToken);

    public Task<ApiResult> DeactivateLocationAsync(Guid id, CancellationToken cancellationToken = default)
        => PostEmptyAsync($"/locations/{id}/deactivate", cancellationToken);
}
