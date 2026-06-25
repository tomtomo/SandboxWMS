using MudBlazor.Services;
using Wms.Platform.Hosting;
using Wms.WebUI;
using Wms.WebUI.Services;

var builder = WebApplication.CreateBuilder(args);

// service defaults: health + service discovery + HTTP resilience + OTel (ADR-0008).
// Service discovery DIPERLUKAN agar base address "http://gateway" ter-resolve.
builder.AddServiceDefaults();

// What: Blazor Web App (InteractiveServer) — idiom .NET 8 (revisit Phase 04e).
// Why: compute rationale ADR-0018 TAK berubah (server interactivity = SignalR circuit always-on);
// yang berubah hanya idiom project template. prerender:false diset di App.razor (circuit-scoped state bersih).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// circuit-scoped auth state + typed API clients (Ports & Adapters UI; ADR-0006).
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<WmsApiClient>();
// circuit-scoped reference-data lookup (Warehouse id→name) — dipakai lintas halaman tanpa re-fetch.
builder.Services.AddScoped<WarehouseNameResolver>();
builder.Services.AddHttpClient("gateway", client => client.BaseAddress = new Uri("http://gateway"));

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();
