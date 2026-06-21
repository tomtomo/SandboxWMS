using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Wms.Platform.Hosting;

// What: Service Defaults (Aspire-style cross-cutting host config, ADR-0008)
// Why: konfigurasi lintas-host (health, service discovery, HTTP resilience) dikunci
// di SATU tempat agnostic — tiap host cukup AddServiceDefaults(), konsisten lintas
// service tanpa cloud SDK. OTel baseline ditambahkan di Phase 02c.
// How: extension di IHostApplicationBuilder agar jalan untuk web-host & worker.
public static class ServiceDefaultsExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        // What: resilience default untuk semua HttpClient (ADR-0020, baseline)
        // Why: outbound HTTP dapat retry/timeout/circuit-breaker standar secara default-on;
        // kalibrasi split-timeout (gRPC vs HTTP) didalami di Phase 07c.
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // liveness: proses hidup & responsif
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        return builder;
    }

    // What: health endpoint mapping (readiness /health + liveness /alive)
    // Why: Aspire & orchestrator (ACA/Cloud Run nanti) probe endpoint ini untuk
    // menentukan resource healthy. Hanya cocok diekspos polos di Local.
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });

        return app;
    }
}
