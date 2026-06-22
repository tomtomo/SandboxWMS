using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.Platform.Hosting;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience + OTel (ADR-0008).
// Service discovery DIPERLUKAN: destination cluster YARP ("http://auth", dst) di-resolve ke endpoint
// Aspire nyata via AddServiceDiscoveryDestinationResolver di bawah.
builder.AddServiceDefaults();

// What: Reverse Proxy (YARP) — edge gateway lokal (ADR-0006 / ADR-0018)
// Why: satu pintu REST dari WebUI ke service; cross-cutting (auth-forward bearer + correlation-id)
// terpusat di edge. TANPA transcoding — tiap service expose REST sendiri (ADR-0006), gateway cuma
// route. Cloud = managed gateway (APIM/Apigee) via IaC (Phase 05c/06c); YARP HANYA lokal.
// How: LoadFromConfig membaca section "ReverseProxy" (routes+clusters di appsettings); destination
// "http://<service>" di-resolve ke host:port Aspire lewat service-discovery resolver YARP.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

// What: correlation-id propagation di edge (ADR-0024) — pastikan X-Correlation-ID ADA sebelum forward
// Why: gateway = origin korelasi bila klien tak menyuplai; YARP default sudah meneruskan header request
// yang ada, jadi menambah di sini saat absen membuat SATU korelator menembus gateway + service hilir.
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey(CorrelationIdMiddleware.HeaderName))
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = Guid.NewGuid().ToString("N");
    await next();
});

// correlation-id middleware (BuildingBlocks): log-scope gateway + echo response header (ADR-0024)
app.UseCorrelationId();

app.MapDefaultEndpoints();

// What: auth-forward bearer (ADR-0006) — TANPA kode khusus: YARP meneruskan header Authorization
// (bukan hop-by-hop) apa adanya ke service hilir, yang memvalidasi JWT offline sendiri (ADR-0016).
app.MapReverseProxy();

app.MapGet("/", () => "Wms.Gateway — Phase 04e (YARP local: routing REST + auth-forward + correlation-id)");

app.Run();
