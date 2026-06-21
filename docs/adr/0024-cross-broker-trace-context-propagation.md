# ADR-0024: Cross-broker trace-context propagation (W3C) di atas messaging async

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Message envelope (`BuildingBlocks.Infrastructure`), adapter broker `Platform.<Cloud>`, consumer; melengkapi telemetry lokal [ADR-0008](0008-aspire-distributed-local.md)

## Context

Aspire/OTel ([ADR-0008](0008-aspire-distributed-local.md)) memberi tracing **dalam satu proses**, tapi berhenti di **hop broker**: saat domain event menyeberang via Outbox → broker → consumer ([ADR-0005](0005-event-driven-outbox.md)), trace terputus — producer dan consumer muncul sebagai dua trace terpisah. Ini bagian tersulit observability EDA dan membuat debugging alur lintas-context (GRConfirmed → Inventory → …) jadi buta.

## Decision

- **Pilihan:** Rambatkan **W3C Trace Context** menembus broker. Saat publish: ambil `traceparent`/`tracestate` dari `Activity.Current`, bawa di **message envelope**. Adapter platform memetakannya ke **properti broker-native** (Service Bus application properties / Pub/Sub attributes). Consumer **restart Activity** dengan parent itu → satu trace end-to-end producer → consumer.
- **Kenapa:** Core hanya membawa string W3C (netral), pemetaan ke broker ada di adapter — fit hexagonal ([ADR-0002](0002-tri-cloud-hexagonal.md)). W3C Trace Context adalah standar lintas-vendor sehingga trace tetap utuh lintas-cloud. `→ Canon: W3C Trace Context spec (traceparent/tracestate); OpenTelemetry, context propagation`.
- **Trade-off:** Setiap adapter broker harus mengimplementasi map in/out; envelope jadi punya field telemetry (kecil).
- **Kapan ditinjau ulang:** Bila pindah ke transport yang sudah meng-handle context propagation natively → mapping bisa disederhanakan.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. W3C di envelope + adapter map + consumer restart Activity** *(dipilih)* | Trace utuh lintas-cloud; core netral | Map per adapter broker | W3C Trace Context; OTel |
| B. Tanpa propagation (status quo) | Nol kerja | Trace putus di broker; debugging async buta | OpenTelemetry (context propagation) |
| C. Vendor-specific correlation per broker | Native | Tak portabel lintas-cloud; bocor ke core | broker-native correlation (mis. Service Bus correlation props) |

## Consequences

**Positif**
- Satu trace producer → consumer untuk alur EDA; korelasi dengan correlation-id (`BuildingBlocks.Web`) makin kuat.
- Payload cert observability (AZ-204 Application Insights / distributed tracing).

**Trade-off / lebih sulit**
- Konsistensi implementasi di tiap adapter broker harus dijaga (kandidat test integrasi).

**Yang harus dijaga**
- Core hanya menyimpan string W3C; pemetaan broker tetap di `Platform.<Cloud>` — jaga zero-SDK core ([ADR-0002](0002-tri-cloud-hexagonal.md), mandat), ditegakkan [ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md) FF #1.

## Out of scope / deferred

- Bisa dikonsolidasi ke satu "messaging-conventions" ADR bersama envelope/ordering-key bila nanti dirasa perlu.
- Sampling strategy & trace backend per-cloud = deploy-time.
