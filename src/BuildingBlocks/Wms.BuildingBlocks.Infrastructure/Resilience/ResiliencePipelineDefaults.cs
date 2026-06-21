using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Wms.BuildingBlocks.Infrastructure.Resilience;

// What: Resilience pipeline defaults factory (Polly v8; ADR-0020) — single source of truth
// Why: integration point = sumber kegagalan paling umum. Stability patterns kanonik (Nygard, Release
// It!) dipusatkan di sini, BUKAN per-callsite ad-hoc (drift). Insight LOAD-BEARING = SPLIT TIMEOUT
// per-transport: gRPC ~30s (toleran cold-start compute scale-to-zero, ADR-0018) vs HTTP ~5s (fail-fast).
// Timeout 5s seragam akan menggagalkan call gRPC pertama ke service dingin di attempt-1, alih-alih
// menyerap cold start. Tabel knob TERKUNCI (ubah = ubah ADR-0020); angka provisional sampai ada
// traffic nyata (kalibrasi Phase 07c). Behavioral FF `split-timeout-configured` menjaga gRPC≠HTTP.
// How: ConfigureGrpcPipeline menyusun strategi urutan CircuitBreaker(outer) → Retry → Timeout(inner,
// per-attempt) — first-added = outermost (Polly v8). AddGrpcResiliencePipeline mendaftar named pipeline
// (di-resolve ResiliencePipelineProvider<string> per-client key). HTTP memakai AddStandardResilienceHandler
// (ServiceDefaults) dengan HttpAttemptTimeout dari sini. Retry+Timeout aman untuk read gRPC (idempotent).
public static class ResiliencePipelineDefaults
{
    // What: per-attempt timeout gRPC — panjang untuk menyerap cold-start scale-to-zero (ADR-0018/0020)
    public static readonly TimeSpan GrpcAttemptTimeout = TimeSpan.FromSeconds(30);

    // What: per-attempt timeout HTTP — fail-fast (ADR-0020), dipakai standard resilience handler
    public static readonly TimeSpan HttpAttemptTimeout = TimeSpan.FromSeconds(5);

    public const int MaxRetryAttempts = 3;

    public static readonly TimeSpan RetryBaseDelay = TimeSpan.FromMilliseconds(200);

    public const double CircuitBreakerFailureRatio = 0.5;

    public static readonly TimeSpan CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);

    public const int CircuitBreakerMinimumThroughput = 10;

    public static readonly TimeSpan CircuitBreakerBreakDuration = TimeSpan.FromSeconds(15);

    // What: key named pipeline gRPC (ResiliencePipelineProvider<string>, ADR-0020)
    public const string GrpcPipelineKey = "wms-grpc";

    // What: susun pipeline gRPC — CircuitBreaker(outer) → Retry → Timeout(inner, per-attempt)
    // Why: urutan ADR-0020. CB outermost → short-circuit saat callee down (tak buang attempt). Retry
    // menyerap transient. Timeout per-attempt innermost = batas tiap percobaan (absorb cold-start).
    public static void ConfigureGrpcPipeline(ResiliencePipelineBuilder builder)
    {
        builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = CircuitBreakerFailureRatio,
                SamplingDuration = CircuitBreakerSamplingDuration,
                MinimumThroughput = CircuitBreakerMinimumThroughput,
                BreakDuration = CircuitBreakerBreakDuration,
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = RetryBaseDelay,
            })
            .AddTimeout(GrpcAttemptTimeout);
    }

    // What: registrasi named gRPC pipeline (ADR-0020) — di-resolve ResiliencePipelineProvider<string>
    // Why: gRPC client (Inbound/Outbound) konsumsi pipeline per-client key, bukan resilience ad-hoc.
    public static IServiceCollection AddGrpcResiliencePipeline(this IServiceCollection services)
    {
        services.AddResiliencePipeline(GrpcPipelineKey, ConfigureGrpcPipeline);
        return services;
    }
}
