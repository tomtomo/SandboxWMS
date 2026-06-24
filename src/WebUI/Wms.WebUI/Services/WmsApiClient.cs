namespace Wms.WebUI.Services;

// What: Composition root Web → REST (ADR-0006) — sub-client per modul, semua via gateway named client.
// Why: satu injection point (@inject WmsApiClient Api → Api.Inbound.ListAsync(...)). Per-modul plumbing di
// Apis/<Module>Api.cs. Sub-client MasterData/Inventory/Outbound ditambah di milestone masing-masing.
public sealed class WmsApiClient(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
{
    public AuthApi Auth { get; } = new(httpClientFactory, tokenStore);
    public MasterDataApi MasterData { get; } = new(httpClientFactory, tokenStore);
    public InboundApi Inbound { get; } = new(httpClientFactory, tokenStore);
    public ReportingApi Reporting { get; } = new(httpClientFactory, tokenStore);
    public NotificationApi Notification { get; } = new(httpClientFactory, tokenStore);
    public InventoryApi Inventory { get; } = new(httpClientFactory, tokenStore);
    public OutboundApi Outbound { get; } = new(httpClientFactory, tokenStore);
}
