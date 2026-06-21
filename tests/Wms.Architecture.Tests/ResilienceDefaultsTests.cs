using Wms.BuildingBlocks.Infrastructure.Resilience;

namespace Wms.Architecture.Tests;

// What: behavioral fitness function `split-timeout-configured` (ADR-0020) — registry ADR-0003
// Why: BUKAN NetArchTest — inspeksi NILAI config runtime. Menjaga insight load-bearing "split timeout
// per-transport" tak luntur: timeout 5s seragam akan menggagalkan call gRPC PERTAMA ke service
// scale-to-zero (cold-start) di attempt-1, alih-alih menyerapnya. gRPC (toleran cold-start) HARUS
// berbeda & lebih panjang dari HTTP (fail-fast), keduanya ter-set.
public class ResilienceDefaultsTests
{
    [Fact]
    public void Split_timeout_configured_grpc_differs_from_http_and_both_set()
    {
        Assert.True(ResiliencePipelineDefaults.GrpcAttemptTimeout > TimeSpan.Zero, "timeout gRPC harus ter-set.");
        Assert.True(ResiliencePipelineDefaults.HttpAttemptTimeout > TimeSpan.Zero, "timeout HTTP harus ter-set.");
        Assert.NotEqual(ResiliencePipelineDefaults.HttpAttemptTimeout, ResiliencePipelineDefaults.GrpcAttemptTimeout);
        // gRPC lebih toleran (absorb cold-start scale-to-zero) daripada HTTP fail-fast
        Assert.True(ResiliencePipelineDefaults.GrpcAttemptTimeout > ResiliencePipelineDefaults.HttpAttemptTimeout);
    }
}
