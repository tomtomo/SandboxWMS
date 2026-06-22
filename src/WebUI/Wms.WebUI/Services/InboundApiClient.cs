using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Inbound (ADR-0006) — create + list GoodsReceipt via gateway
public sealed class InboundApiClient(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    public async Task<PagedResultDto<GoodsReceiptListRow>> ListAsync(
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => await CreateClient().GetFromJsonAsync<PagedResultDto<GoodsReceiptListRow>>(
               $"/goods-receipts?page={page}&pageSize={pageSize}", cancellationToken)
           ?? new PagedResultDto<GoodsReceiptListRow>([], page, pageSize, 0);

    public async Task<(bool Ok, string Message)> CreateAsync(
        string warehouseId, string? poRef, string? supplierId,
        IReadOnlyList<CreateLine> expectedLines, CancellationToken cancellationToken = default)
    {
        var payload = new { warehouseId, poRef, supplierId, expectedLines };
        var response = await CreateClient().PostAsJsonAsync("/goods-receipts", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, $"Create GR gagal ({(int)response.StatusCode}).");

        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(cancellationToken);
        return (true, $"GR dibuat: {created?.Id}");
    }
}

// What: read DTO (bentuk respons GET /goods-receipts) — selaras GoodsReceiptListItem Inbound
public sealed record GoodsReceiptListRow(
    Guid GrId, string WarehouseId, string? PoRef, string? SupplierId,
    string Status, int ExpectedLineCount, DateTimeOffset CreatedAt);

public sealed record CreateLine(string Sku, int ExpectedQty);

internal sealed record CreatedResponse(Guid Id);
