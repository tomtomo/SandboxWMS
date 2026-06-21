using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Wms.BuildingBlocks.Infrastructure.Resilience;

namespace Wms.Platform.Hosting;

// What: Service Defaults (Aspire-style cross-cutting host config, ADR-0008)
// Why: konfigurasi lintas-host (health, service discovery, HTTP resilience, OTel) dikunci
// di SATU tempat agnostic — tiap host cukup AddServiceDefaults(), konsisten lintas
// service tanpa cloud SDK. OTel vendor-neutral (W3C/OTLP) → portabel lintas-cloud.
// How: extension di IHostApplicationBuilder agar jalan untuk web-host & worker.
public static class ServiceDefaultsExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.ConfigureDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        // What: resilience default untuk semua HttpClient (ADR-0020) — sisi HTTP dari split-timeout
        // Why: outbound HTTP dapat retry/timeout/circuit-breaker standar default-on, dengan
        // attempt-timeout FAIL-FAST ~5s (ResiliencePipelineDefaults.HttpAttemptTimeout, single source) —
        // kontras gRPC ~30s (toleran cold-start, di-wire per gRPC client). Phase 04a MEN-SET default
        // split-timeout; kalibrasi angka dgn traffic nyata = Phase 07c.
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler(options =>
                options.AttemptTimeout.Timeout = ResiliencePipelineDefaults.HttpAttemptTimeout);
            http.AddServiceDiscovery();
        });

        return builder;
    }

    // What: OpenTelemetry baseline — traces + metrics + logs (ADR-0008; ADR-0024 baseline)
    // Why: observability terpusat di service-defaults supaya SEMUA host memancarkan telemetry
    // seragam tanpa cloud SDK. Ini fondasi: trace dalam-proses + korelasi (correlation-id),
    // BUKAN W3C cross-broker penuh (ADR-0024 → Phase 07b). Vendor-neutral: ekspor via OTLP,
    // backend (Aspire dashboard lokal / App Insights / Cloud Trace) = keputusan deploy-time.
    // How: logging OTel (formatted message + scopes → correlation-id ikut) + metrics (ASP.NET/
    // HttpClient/runtime) + tracing (ASP.NET/HttpClient + ActivitySource app); exporter OTLP
    // aktif saat OTEL_EXPORTER_OTLP_ENDPOINT diset (Aspire menyetelnya otomatis per resource).
    private static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        // OTLP exporter hanya saat endpoint terkonfigurasi (Aspire inject OTEL_EXPORTER_OTLP_ENDPOINT)
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

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
