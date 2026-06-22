using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Reporting (ADR-0006 / ADR-0017) — stock-on-hand + receiving summary via gateway
// Why: endpoint kini paginated (PagedResult); UI dashboard menampilkan page pertama (.Items). Kontrol
// paging UI penuh = enhancement lanjutan.
public sealed class ReportingApiClient(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    public async Task<IReadOnlyList<StockOnHandRow>> StockOnHandAsync(CancellationToken cancellationToken = default)
        => (await CreateClient().GetFromJsonAsync<PagedResultDto<StockOnHandRow>>(
               "/reports/stock-on-hand", cancellationToken))?.Items ?? [];

    public async Task<IReadOnlyList<ReceivingSummaryRow>> ReceivingSummaryAsync(CancellationToken cancellationToken = default)
        => (await CreateClient().GetFromJsonAsync<PagedResultDto<ReceivingSummaryRow>>(
               "/reports/receiving-summary", cancellationToken))?.Items ?? [];
}

// What: read DTO (bentuk respons query Reporting) — selaras DTO ReportingEndpoints
public sealed record StockOnHandRow(string WarehouseId, string Sku, string Batch, int QtyOnHand);

public sealed record ReceivingSummaryRow(
    string SupplierId, DateOnly Day, int GrCount, int ReceivedQty, int RejectedQty, double DiscrepancyRate);
