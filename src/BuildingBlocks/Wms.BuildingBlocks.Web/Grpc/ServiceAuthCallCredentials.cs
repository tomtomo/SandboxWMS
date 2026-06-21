using System.Diagnostics;
using Grpc.Core;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Web.Correlation;

namespace Wms.BuildingBlocks.Web.Grpc;

// What: gRPC client call-credentials (ADR-0021) — sisip bearer s2s + correlation ke metadata outbound
// Why: hop gRPC internal ([ADR-0006]) harus terautentikasi: OAuth2 bearer AUDIENCE-SCOPED per callee
// (ADR-0021) yang di-mint platform (Local stub: kosong) — diperoleh via port IServiceTokenProvider.
// Plus correlation-id (ADR-0024 baseline) supaya log/trace callee tertaut ke alur caller. Dipakai
// CallCredentials.FromInterceptor (AsyncAuthInterceptor) = ASYNC-NATIVE → tak blocking hot-path (lebih
// benar daripada Interceptor sinkron yang harus .GetAwaiter().GetResult() token async).
// How: callback per-call menambah header Authorization (bila token non-kosong) + X-Correlation-ID
// (dari Activity.Current tag). Sisi server memvalidasi token offline (ADR-0021) & map Result→status via
// ResultExceptionInterceptor. CATATAN Local: channel insecure (h2c) butuh host meng-set
// UnsafeUseInsecureChannelCallCredentials=true agar credentials terkirim (cloud TLS: otomatis).
public static class ServiceAuthCallCredentials
{
    // What: Factory — CallCredentials audience-scoped untuk satu callee (ADR-0021)
    public static CallCredentials Create(IServiceTokenProvider tokenProvider, string audience) =>
        CallCredentials.FromInterceptor(async (context, metadata) =>
        {
            var token = await tokenProvider.GetTokenAsync(audience);
            if (!string.IsNullOrEmpty(token))
                metadata.Add("Authorization", $"Bearer {token}");

            if (Activity.Current?.GetTagItem("correlation.id") is string correlationId
                && !string.IsNullOrEmpty(correlationId))
                metadata.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        });
}
