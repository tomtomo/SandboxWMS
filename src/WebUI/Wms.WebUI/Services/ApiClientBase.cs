using System.Net.Http.Headers;

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
}
