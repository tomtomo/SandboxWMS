using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Inbound (ADR-0006) — create + list GoodsReceipt via gateway.
public sealed class InboundApi(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    public async Task<PagedResultDto<GoodsReceiptListRow>> ListAsync(
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => await CreateClient().GetFromJsonAsync<PagedResultDto<GoodsReceiptListRow>>(
               $"/goods-receipts?page={page}&pageSize={pageSize}", JsonDefaults.Web, cancellationToken)
           ?? new PagedResultDto<GoodsReceiptListRow>([], page, pageSize, 0);

    public Task<ApiResult<Guid>> CreateAsync(
        string warehouseId, string? poRef, string? supplierId,
        IReadOnlyList<CreateLine> expectedLines, CancellationToken cancellationToken = default)
    {
        var payload = new { warehouseId, poRef, supplierId, expectedLines };
        return PostCreateAsync("/goods-receipts", payload, cancellationToken);
    }

    // ---- detail (GAP-aware): GET /goods-receipts/{id} ----
    public Task<ApiResult<GoodsReceiptDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => GetOneAsync<GoodsReceiptDetailDto>($"/goods-receipts/{id}", "🚧 GR detail API belum tersedia (lihat webui-api-gaps.md).", cancellationToken);

    // ---- attachments list (GAP-aware): GET /goods-receipts/{id}/attachments ----
    public async Task<ApiResult<IReadOnlyList<AttachmentDto>>> ListAttachmentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await CreateClient().GetAsync($"/goods-receipts/{id}/attachments", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return ApiResult<IReadOnlyList<AttachmentDto>>.Fail("🚧 Attachment list API belum tersedia (lihat webui-api-gaps.md).");
        if (!response.IsSuccessStatusCode)
            return ApiResult<IReadOnlyList<AttachmentDto>>.Fail($"Gagal memuat attachment ({(int)response.StatusCode}).");
        var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<AttachmentDto>>(JsonDefaults.Web, cancellationToken);
        return ApiResult<IReadOnlyList<AttachmentDto>>.Ok(list ?? []);
    }

    // ---- workflow commands (functional) ----
    public Task<ApiResult> ScanAsync(Guid id, ScanLineForm form, CancellationToken cancellationToken = default)
        => PostJsonAsync($"/goods-receipts/{id}/scans", new
        {
            form.Sku, form.ActualQty, form.Batch, form.Expiry,
            LineStatus = form.LineStatus.ToString(), // string enum-name (host JsonStringEnumConverter)
        }, cancellationToken);

    public Task<ApiResult> CompleteScanAsync(Guid id, CancellationToken cancellationToken = default)
        => PostEmptyAsync($"/goods-receipts/{id}/scan-complete", cancellationToken);

    public Task<ApiResult> ConfirmAsync(Guid id, CancellationToken cancellationToken = default)
        => PostEmptyAsync($"/goods-receipts/{id}/confirm", cancellationToken);

    public Task<ApiResult> HoldAsync(Guid id, HoldGoodsReceiptForm form, CancellationToken cancellationToken = default)
        => PostJsonAsync($"/goods-receipts/{id}/hold", new { form.Reason }, cancellationToken);

    // resolve identitas = Sku + Type (string enum-name), BUKAN id
    public Task<ApiResult> ResolveDiscrepancyAsync(
        Guid id, string sku, string type, ResolveDiscrepancyForm form, CancellationToken cancellationToken = default)
        => PostJsonAsync($"/goods-receipts/{id}/discrepancies/resolve", new
        {
            Sku = sku,
            Type = type,
            Action = form.Action!.Value.ToString(),
            form.Note,
        }, cancellationToken);

    public async Task<ApiResult<Guid>> UploadAttachmentAsync(
        Guid id, byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName); // field "file" wajib cocok IFormFile param endpoint
        var response = await CreateClient().PostAsync($"/goods-receipts/{id}/attachments", multipart, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ApiResult<Guid>.Fail(await SafeReadAsync(response, cancellationToken));
        var body = await response.Content.ReadFromJsonAsync<CreatedIdResponse>(JsonDefaults.Web, cancellationToken);
        return body is null ? ApiResult<Guid>.Fail("Respons upload tak terbaca.") : ApiResult<Guid>.Ok(body.Id);
    }
}

// What: read DTO (bentuk respons GET /goods-receipts) — selaras GoodsReceiptListItem Inbound
public sealed record GoodsReceiptListRow(
    Guid GrId, string WarehouseId, string? PoRef, string? SupplierId,
    string Status, int ExpectedLineCount, DateTimeOffset CreatedAt);

public sealed record CreateLine(string Sku, int ExpectedQty);
