# ADR-0025: Cross-cutting platform ports — delayed-task & telemetry-stream (reliability-degree taxonomy)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Port di `BuildingBlocks.Application`, adapter `Platform.<Cloud>`; konsumen Notification/Reporting ([ADR-0017](0017-eventual-consistency-reporting-notification.md))

## Context

Dua kebutuhan cross-cutting belum punya rumah di 18 ADR maupun seedwork: (1) **trigger berbasis waktu** — mis. quarantine-aging yang harus me-raise `StockQuarantineStale` lewat timer; `System.Timers.Timer` in-proc hilang saat restart, dan SDK scheduler cloud di core melanggar zero-SDK. (2) **telemetry-stream** — sinyal yang boleh hilang, beda kontrak keandalan dari Outbox at-least-once.

## Decision

- **Pilihan:** Dua **CloudPorts** baru dengan kontrak keandalan eksplisit:
  - **`IDelayedTaskQueue` + `IDelayedTaskHandler`** — trigger time-driven durable (anchor: quarantine-aging → `StockQuarantineStale`).
  - **`ITelemetryStream`** — seam **fire-and-forget soft**; fail-open (swallow failure, **tak pernah** return `Failure`/throw) adalah **invariant port**, eksplisit beda dari at-least-once Outbox ([ADR-0005](0005-event-driven-outbox.md)).
  - **Framing taksonomi**: satu shape CloudPorts pada **tiga derajat keandalan** — per-attempt retry (kontrak resilience [ADR-0020](0020-resilience-pipeline-defaults.md); channel delivery Notification [ADR-0017](0017-eventual-consistency-reporting-notification.md)) · durable scheduling (delayed-task) · fail-open soft (telemetry).
- **Kenapa:** Event yang dibangkitkan timer tak punya rumah saat ini; port menjaga mekanisme di balik abstraksi tri-cloud-swappable ([ADR-0002](0002-tri-cloud-hexagonal.md)). Taksonomi keandalan memberi tempat otoritatif untuk menyatakan "sinyal ini BOLEH hilang" vs "TIDAK BOLEH". `→ Canon: Cockburn/Hombergs (Hexagonal), ports; Hohpe & Woolf (EIP), channel semantics; MS Learn: Azure Service Bus scheduled messages (ScheduledEnqueueTimeUtc) / Google Cloud Tasks + OIDC`.
- **Trade-off:** Menambah port + adapter per cloud; derajat keandalan harus dipilih sadar per penggunaan.
- **Kapan ditinjau ulang:** Bila muncul kebutuhan scheduling yang lebih kaya (cron, windowing) → pertimbangkan scheduler khusus (mis. Hangfire) di balik port yang sama.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Port delayed-task + telemetry-stream + taksonomi keandalan** *(dipilih)* | Trigger waktu punya rumah; keandalan eksplisit; tri-cloud bersih | Lebih banyak port/adapter | Hohpe & Woolf (EIP); MS Learn |
| B. `System.Timers.Timer` in-proc | Nol infra | Hilang saat restart; tak skala; SDK cloud bocor kalau pakai scheduler cloud | Cockburn (Hexagonal) |
| C. Pakai broker biasa untuk delay (ADR-0005) | Reuse rails | Bukan mekanisme delay/schedule; semantik salah | Hohpe & Woolf (EIP) |

## Consequences

**Positif**
- `StockQuarantineStale` & alert berbasis-umur lain ([ADR-0017](0017-eventual-consistency-reporting-notification.md) Notification) punya pemicu durable yang survive restart.
- Taksonomi keandalan memperjelas kontrak tiap CloudPort (channel/delayed/telemetry).
- Payload cert: Azure Service Bus scheduled message (ScheduledEnqueueTimeUtc), GCP Cloud Tasks + OIDC.

**Trade-off / lebih sulit**
- Adapter durable cloud (Queue Storage/Cloud Tasks) belum dibangun di awal.

**Yang harus dijaga**
- Invariant `ITelemetryStream` = fail-open (tak boleh memengaruhi alur bisnis); `IDelayedTaskQueue` di balik port (zero-SDK core).

## Out of scope / deferred

- Adapter durable cloud (Queue Storage/Cloud Tasks/Event Hubs/Pub-Sub) → mulai Local in-memory/log-only; cloud menyusul saat ada use-case butuh restart-survival.
- WaveSaga allocation-timeout use-case → **dikecualikan** (saga deferred, [ADR-0005](0005-event-driven-outbox.md)).
- `IReactiveEventPublisher` ops-eventing 3-way → ditolak (tumpang-tindih domain event + notif channel).
