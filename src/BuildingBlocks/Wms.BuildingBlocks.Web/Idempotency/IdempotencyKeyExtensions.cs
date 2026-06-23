using Microsoft.AspNetCore.Builder;

namespace Wms.BuildingBlocks.Web.Idempotency;

// What: composition middleware Idempotency-Key (ADR-0032)
// Why: host cukup app.UseIdempotencyKey() — posisi dikunci (setelah UseCorrelationId, sebelum
// UseAuthentication) konsisten lintas host saat rollout.
public static class IdempotencyKeyExtensions
{
    public static IApplicationBuilder UseIdempotencyKey(this IApplicationBuilder app)
        => app.UseMiddleware<IdempotencyKeyMiddleware>();
}
