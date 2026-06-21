# Phase 07b — Observability: Distributed Tracing Cross-Broker (W3C)

**Status:** planned

**Pre-conditions:**
- **07a done:** authZ aktif lintas sistem (dua cloud). OTel baseline lokal (02c) + Aspire dashboard ada; adapter broker `Platform.Azure` (Service Bus) & `Platform.Gcp` (Pub/Sub) sudah memetakan envelope (05a/06a), tapi trace masih putus di hop broker.
- Bagian **Phase 07 Cross-Cutting Wide (FINAL)** — DEEP pass observability di atas baseline 02c.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0024-cross-broker-trace-context-propagation.md` (W3C `traceparent`/`tracestate` → broker-native props → consumer restart Activity)
- `docs/adr/0025-cross-cutting-platform-ports.md` (`ITelemetryStream` fail-open invariant — swallow, tak pernah return `Failure`/throw)
- `docs/adr/0008-aspire-distributed-local.md` (OTel in-proc baseline yang berhenti di hop broker)

**Tujuan:** Sambung trace yang putus di broker (ADR-0024): satu trace end-to-end producer → broker → consumer di **kedua cloud**; wire backend trace per-cloud (App Insights / Cloud Trace); tambah port `ITelemetryStream` dengan invariant fail-open.

**Deliverable:**
- Propagasi **W3C Trace Context** penuh cross-broker: publish menyalin `traceparent`/`tracestate` dari `Activity.Current` → envelope → **Service Bus application properties** (Azure) / **Pub/Sub attributes** (GCP); consumer **restart Activity** dengan parent itu → satu trace producer→consumer.
- App Insights (Azure) + Cloud Trace (GCP) ter-wire sebagai OTel exporter per-cloud host (deploy-time, di `Platform.<Cloud>`/Hosts — core tetap nol SDK).
- Port **`ITelemetryStream`** (`BuildingBlocks.Application`) + adapter per-cloud (Azure/GCP) + Local; **fail-open invariant**: provider failure di-swallow, tak pernah `Failure`/throw.
- Korelasi structured logging: correlation-id (`BuildingBlocks.Web`) hadir di log terstruktur, terhubung ke trace.
- Cross-adapter trace integration test.

**Tasks:**
1. Map-out (publish side): di adapter Service Bus & Pub/Sub, salin `traceparent`/`tracestate` dari `Activity.Current` ke broker-native props (lengkapi map dari 05a/06a).
2. Map-in (consume side): adapter baca props → `ActivityContext` → **restart Activity** dengan parent → handler jalan di dalam span anak.
3. Wire OTel exporter: App Insights di Azure host, Cloud Trace di GCP host (connection via `ISecretProvider`/config, bukan core).
4. Definisikan port `ITelemetryStream` + adapter Azure/GCP/Local; implement fail-open (try/swallow, log-only).
5. Korelasikan structured logging: pastikan correlation-id ter-enrich di setiap scope log dan selaras dengan trace/span id.
6. Cross-adapter trace integration test: publish→consume melintasi adapter → assert satu trace id konsisten producer→consumer.
7. Behavioral test `ITelemetryStream` fail-open: stub provider yang throw → assert caller TIDAK menerima exception/`Failure` dan alur bisnis lanjut.

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau (FF existing + cross-adapter trace test) + **behavioral test** `ITelemetryStream` fail-open (provider failure di-swallow).
- **Satu trace** menjangkau producer → broker → consumer terlihat di **App Insights (Azure)** DAN **Cloud Trace (GCP)** (smoke per cloud).
- Correlation-id hadir di structured log dan terhubung ke trace.

**Learning objective:** W3C Trace Context propagation lintas broker async; OpenTelemetry → App Insights / Cloud Trace; fail-open telemetry seam (reliability-degree taxonomy, ADR-0025); distributed tracing end-to-end.

**Handoff notes:** Trace EDA utuh producer→consumer di dua cloud; `ITelemetryStream` ter-realisasi dengan invariant fail-open terkunci; correlation-id ↔ trace nyambung. **07c** (resilience) & **07d** (security-hardening) di-observe lewat tracing ini. Sampling strategy per-cloud = deploy-time knob.

**Touchpoint cert:** AZ-204 — Application Insights / distributed tracing / OpenTelemetry → X. PCD — Cloud Trace / Cloud Monitoring / context propagation → X.
