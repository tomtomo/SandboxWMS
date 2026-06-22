using MudBlazor.Services;
using Wms.Platform.Hosting;
using Wms.WebUI.Services;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience + OTel (ADR-0008).
// Service discovery DIPERLUKAN agar base address "http://gateway" (HttpClient di bawah) ter-resolve.
builder.AddServiceDefaults();

// What: Blazor Server (SignalR circuit) — UI stateful (ADR-0018)
// Why: thin UI; interaktivitas via circuit server-side. Stateful → di cloud butuh compute always-on
// (App Service / Cloud Run min-instances≥1 + session affinity, Phase 05c/06c). render-mode "Server"
// (bukan prerender, lihat _Host.cshtml) supaya state circuit-scoped (TokenStore) bersih satu jalur.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// What: circuit-scoped auth state + typed API clients (Ports & Adapters di sisi UI; ADR-0006)
// Why: UI memanggil REST HANYA lewat gateway (tak pernah gRPC/module ref langsung); JWT disimpan
// per-circuit lalu di-attach tiap request. Base address "http://gateway" = resource Aspire (service
// discovery); HTTP resilience default dari AddServiceDefaults berlaku ke named client ini.
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<InboundApiClient>();
builder.Services.AddScoped<ReportingApiClient>();
builder.Services.AddScoped<NotificationApiClient>();
builder.Services.AddHttpClient("gateway", client => client.BaseAddress = new Uri("http://gateway"));

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapDefaultEndpoints();

app.Run();
