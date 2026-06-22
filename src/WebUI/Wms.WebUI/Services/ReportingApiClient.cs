using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Reporting (ADR-0006 / ADR-0017) — stock-on-hand + receiving summary via gateway
public sealed class ReportingApiClient(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    public async Task<IReadOnlyList<StockOnHandRow>> StockOnHandAsync(CancellationToken cancellationToken = default)
        => await CreateClient().GetFromJsonAsync<List<StockOnHandRow>>("/reports/stock-on-hand", cancellationToken)
           ?? [];

    public async Task<IReadOnlyList<ReceivingSummaryRow>> ReceivingSummaryAsync(CancellationToken cancellationToken = default)
        => await CreateClient().GetFromJsonAsync<List<ReceivingSummaryRow>>("/reports/receiving-summary", cancellationToken)
           ?? [];
}

// What: read DTO (bentuk respons query Reporting) — selaras DTO ReportingEndpoints
public sealed record StockOnHandRow(string WarehouseId, string Sku, string Batch, int QtyOnHand);

public sealed record ReceivingSummaryRow(
    string SupplierId, DateOnly Day, int GrCount, int ReceivedQty, int RejectedQty, double DiscrepancyRate);
