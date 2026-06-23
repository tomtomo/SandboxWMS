namespace Wms.BuildingBlocks.Application.Idempotency;

// What: Idempotency record + port DTO untuk IApiIdempotencyStore (ADR-0032)
// Why: mutating REST retry-safe — response sukses pertama disimpan per (endpoint, key) lalu di-replay saat
// retry (Idempotent Receiver, EIP). Tinggal di Application (bahasa port) supaya adapter Platform.<cloud>
// meng-implement tanpa reference BuildingBlocks.Infrastructure (FF cross-layer) — mirror AuditLogEntry/
// DeadLetterMessage. EF mapping-nya di Infrastructure (ApiIdempotencyModel); tipe tetap POCO bersih.
// How: class (EF change-tracking aman); composite natural key (endpoint, idempotency_key) = in-flight guard.
public sealed class ApiIdempotencyRecord
{
    // identitas endpoint mutating ("{METHOD} {path}", mis. "POST /warehouses")
    public string Endpoint { get; init; } = null!;

    // nilai header Idempotency-Key dari client (client bertanggung jawab atas keunikan, RFC 9110)
    public string IdempotencyKey { get; init; } = null!;

    // status HTTP response sukses yang di-cache (2xx) — di-replay saat retry
    public int StatusCode { get; init; }

    // body response (JSON string; null/empty utk 204) — di-replay verbatim
    public string? ResponseBody { get; init; }

    // waktu rekam — dasar TTL cleanup (~24h, job/ops)
    public DateTimeOffset RecordedAt { get; init; }

    // W3C traceparent (Activity.Current) — korelasi audit/trace side-effect idempotency
    public string? Traceparent { get; init; }
}
