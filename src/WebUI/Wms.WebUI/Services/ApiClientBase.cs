using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: base API client (Ports & Adapters di sisi UI) — pembungkus HttpClient ke gateway (ADR-0006)
// Why: tiap call REST butuh bearer (TokenStore circuit) + correlation-id; plumbing disentralkan di sini
// supaya tiap domain-client (Auth/Inbound/Reporting/Notification) tak menduplikasi.
// How: named client "gateway" (base address service-discovery) di-create per call; Authorization +
// X-Correlation-ID diset di client baru — BUKAN DelegatingHandler (hindari gotcha lifetime handler-pool
// vs TokenStore circuit-scoped di Blazor Server).
public abstract class ApiClientBase(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
{
    // header korelasi selaras BuildingBlocks.Web (CorrelationIdMiddleware.HeaderName) — UI = origin korelator
    private const string CorrelationHeader = "X-Correlation-ID";

    protected TokenStore TokenStore { get; } = tokenStore;

    protected HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("gateway");
        if (TokenStore.AccessToken is { } token)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add(CorrelationHeader, Guid.NewGuid().ToString("N"));
        return client;
    }

    // What: shared REST helpers (DRY) — dipakai semua sub-client. gapMessage != null → 404 dipetakan ke
    // pesan graceful-degrade; null → 404 jatuh ke jalur error biasa (endpoint functional).
    protected async Task<ApiResult<PagedResultDto<T>>> GetPagedAsync<T>(
        string path, string? gapMessage, CancellationToken cancellationToken)
    {
        var response = await CreateClient().GetAsync(path, cancellationToken);
        if (gapMessage is not null && response.StatusCode == HttpStatusCode.NotFound)
            return ApiResult<PagedResultDto<T>>.Fail(gapMessage);
        if (!response.IsSuccessStatusCode)
            return ApiResult<PagedResultDto<T>>.Fail($"Gagal memuat ({(int)response.StatusCode}).");
        var page = await response.Content.ReadFromJsonAsync<PagedResultDto<T>>(JsonDefaults.Web, cancellationToken);
        return page is null
            ? ApiResult<PagedResultDto<T>>.Fail("Respons tak terbaca.")
            : ApiResult<PagedResultDto<T>>.Ok(page);
    }

    protected async Task<ApiResult<T>> GetOneAsync<T>(
        string path, string? gapMessage, CancellationToken cancellationToken)
    {
        var response = await CreateClient().GetAsync(path, cancellationToken);
        if (gapMessage is not null && response.StatusCode == HttpStatusCode.NotFound)
            return ApiResult<T>.Fail(gapMessage);
        if (!response.IsSuccessStatusCode)
            return ApiResult<T>.Fail($"Gagal memuat ({(int)response.StatusCode}).");
        var dto = await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Web, cancellationToken);
        return dto is null ? ApiResult<T>.Fail("Respons tak terbaca.") : ApiResult<T>.Ok(dto);
    }

    protected async Task<ApiResult<Guid>> PostCreateAsync(string path, object body, CancellationToken cancellationToken)
    {
        var response = await CreateClient().PostAsJsonAsync(path, body, JsonDefaults.Web, cancellationToken);
        if (!response.IsSuccessStatusCode) return ApiResult<Guid>.Fail(await SafeReadAsync(response, cancellationToken));
        var created = await response.Content.ReadFromJsonAsync<CreatedIdResponse>(JsonDefaults.Web, cancellationToken);
        return created is null ? ApiResult<Guid>.Fail("Respons create tak terbaca.") : ApiResult<Guid>.Ok(created.Id);
    }

    protected async Task<ApiResult> PostJsonAsync(string path, object body, CancellationToken cancellationToken)
    {
        var response = await CreateClient().PostAsJsonAsync(path, body, JsonDefaults.Web, cancellationToken);
        return response.IsSuccessStatusCode ? ApiResult.Ok() : ApiResult.Fail(await SafeReadAsync(response, cancellationToken));
    }

    protected async Task<ApiResult> PostEmptyAsync(string path, CancellationToken cancellationToken)
    {
        var response = await CreateClient().PostAsync(path, content: null, cancellationToken);
        return response.IsSuccessStatusCode ? ApiResult.Ok() : ApiResult.Fail(await SafeReadAsync(response, cancellationToken));
    }

    // FIX (review): rethrow OperationCanceledException — jangan ditelan catch-all.
    protected static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}" : body;
        }
        catch (OperationCanceledException) { throw; }
        catch { return $"HTTP {(int)response.StatusCode}"; }
    }
}

// What: envelope respons create endpoint ({ id }) — dipakai PostCreateAsync (Inbound/Outbound).
internal sealed record CreatedIdResponse(Guid Id);
