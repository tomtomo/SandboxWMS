using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Wms.BuildingBlocks.Web.Correlation;

// What: correlation-id middleware (ADR-0024 baseline)
// Why: satu id stabil per alur request menautkan SEMUA log + span jadi satu cerita — fondasi
// observability sebelum W3C trace-context cross-broker penuh (ADR-0024 → Phase 07b). Caller
// boleh men-supply id (lintas service via header); bila tak ada, di-derive dari trace aktif
// supaya audit/log/trace berbagi korelator yang sama.
// How: baca header X-Correlation-ID (atau fallback ke Activity.TraceId / Guid baru) → tandai
// Activity.Current (muncul di trace) + push ke logging scope (muncul di tiap log) + echo balik
// di response header agar klisien/hop berikut bisa meneruskannya.
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var incoming) && !StringValues.IsNullOrEmpty(incoming)
                ? incoming.ToString()
                : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        Activity.Current?.SetTag("correlation.id", correlationId);
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
