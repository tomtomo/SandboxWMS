namespace Wms.BuildingBlocks.Application.Messaging;

// What: Message Envelope (EIP) + cross-broker trace-context seam (ADR-0024)
// Why: metadata transport (id, waktu, nama logical, trace) dipisah dari payload
// bisnis supaya rail messaging netral terhadap broker konkret. traceparent/tracestate
// (W3C Trace Context) ikut di sini agar trace tetap utuh menembus hop broker —
// adapter per-cloud memetakannya ke properti broker-native (Service Bus app
// properties / Pub/Sub attributes). Core hanya menyimpan string W3C, nol cloud SDK.
// How: record immutable; Payload = integration event ter-serialize (JSON string) —
// envelope tak kenal tipe CLR-nya, menjaga producer/consumer decoupled (ADR-0005).
public sealed record MessageEnvelope(
    Guid EventId,
    string LogicalName,
    DateTimeOffset OccurredAt,
    string Payload,
    string? Traceparent,
    string? Tracestate);
