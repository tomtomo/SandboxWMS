using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Application.Idempotency;

namespace Wms.BuildingBlocks.Web.Idempotency;

// What: middleware Idempotency-Key (ADR-0032) — retry-safe mutating REST (EIP Idempotent Receiver di API)
// Why: request mutating (POST/PUT/PATCH/DELETE) ber-header Idempotency-Key → cek store: HIT replay status+
// body tersimpan TANPA eksekusi handler (retry tak menduplikasi operasi / tak dapat 409 menyesatkan); MISS
// eksekusi → capture response SUKSES (2xx) → simpan → kirim ke client. Store SYNCHRONOUS dalam request scope
// (BUKAN fire-and-forget: DbContext scoped akan ter-dispose) tapi BEST-EFFORT (gagal-simpan di-log, tak
// menggagalkan response — retry berikutnya MISS → re-eksekusi, aman). Posisi: setelah UseCorrelationId,
// sebelum UseAuthentication. GET & request tanpa header pass-through nol-biaya.
// How: swap Response.Body ke MemoryStream; next(); salin buffer ke body asli; bila 2xx & ≤cap → simpan.
// JSON/text-only (cap MaxBodyChars); body besar/biner → tak di-cache (retry re-eksekusi).
public sealed class IdempotencyKeyMiddleware(RequestDelegate next, ILogger<IdempotencyKeyMiddleware> logger)
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaxBodyChars = 8192;

    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
        { HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete };

    public async Task InvokeAsync(HttpContext context, IApiIdempotencyStore store)
    {
        if (!MutatingMethods.Contains(context.Request.Method)
            || !context.Request.Headers.TryGetValue(HeaderName, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            await next(context);
            return;
        }

        var key = keyValues.ToString();
        var endpoint = $"{context.Request.Method} {context.Request.Path}";

        // HIT: replay status + body asli, tanpa eksekusi handler (retry-safe)
        var cached = await store.TryGetAsync(endpoint, key, context.RequestAborted);
        if (cached is not null)
        {
            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = "application/json";
            if (!string.IsNullOrEmpty(cached.ResponseBody))
                await context.Response.WriteAsync(cached.ResponseBody, context.RequestAborted);
            return;
        }

        // MISS: eksekusi + capture response (buffer Response.Body)
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);

            buffer.Position = 0;
            var bodyText = Encoding.UTF8.GetString(buffer.ToArray());
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);

            // simpan HANYA response sukses (2xx) ≤ cap — gagal/transient TAK di-cache (retry re-eksekusi)
            if (context.Response.StatusCode is >= 200 and < 300 && bodyText.Length <= MaxBodyChars)
                await TryStoreAsync(store, endpoint, key, context.Response.StatusCode, bodyText);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    // simpan response best-effort: in-flight duplicate (composite PK) / gagal-simpan → log, response sudah dilayani
    private async Task TryStoreAsync(
        IApiIdempotencyStore store, string endpoint, string key, int statusCode, string body)
    {
        try
        {
            await store.StoreAsync(new ApiIdempotencyRecord
            {
                Endpoint = endpoint,
                IdempotencyKey = key,
                StatusCode = statusCode,
                ResponseBody = body,
                RecordedAt = DateTimeOffset.UtcNow,
                Traceparent = Activity.Current?.Id,
            });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Idempotency store gagal {Endpoint} {Key} — response sudah dikirim; retry akan re-eksekusi.",
                endpoint, key);
        }
    }
}
