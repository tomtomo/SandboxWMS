using Microsoft.AspNetCore.Builder;

namespace Wms.BuildingBlocks.Web.Correlation;

// What: composition middleware correlation-id (ADR-0024 baseline)
// Why: host HTTP cukup app.UseCorrelationId() di awal pipeline — dikunci di satu tempat agar
// posisi (sedini mungkin, setelah Activity request lahir) konsisten lintas service.
public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
