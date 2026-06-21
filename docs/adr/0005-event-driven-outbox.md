# ADR-0005: Event-Driven — domain event → integration event → Outbox → broker

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Antar bounded context (Inbound↔Inventory↔Outbound; Reporting/Notification sebagai consumer)

## Context

Modul tak boleh saling memanggil langsung (ADR-0001/0010). Koordinasi lintas-context (`GRConfirmed`, `WaveReleased`, `StockAllocated`, `ShipmentDispatched`) butuh mekanisme yang **decoupled** dan **andal di hadapan partial failure**. Bahaya klasik: state ter-commit tapi event gagal terbit (atau sebaliknya) → dual-write inconsistency.

## Decision

- **Pilihan:** **Event-Driven Architecture**. `IDomainEvent` bersifat **in-process** di transaksi aggregate, lalu **diterjemahkan** jadi **integration event ber-versi** (`<Module>.Contracts`). Hanya integration event yang **dipersist via Outbox** (satu transaksi dengan state) lalu **dipublish ke broker**. Di sisi consumer, idempotency (Inbox) + Anti-Corruption Layer menerjemahkan contract asing ke model sendiri.
- **Kenapa:** Outbox menjamin atomicity state+event (anti dual-write). Pemisahan domain event (internal) vs integration event (wire) menjaga model internal bebas berevolusi tanpa memecah konsumen. `→ Canon: Hohpe & Woolf (EIP), Guaranteed Delivery & Message Translator; Bellemare (Event-Driven Microservices), event schema & evolution; Vernon (IDDD), domain event sebagai integrasi`.
- **Trade-off:** Eventual consistency lintas-context; infrastruktur Outbox/Inbox + relay harus dibangun & dimonitor.
- **Kapan ditinjau ulang:** Bila ada alur yang butuh **strong consistency** lintas-context (tak ada di scope sekarang) → pertimbangkan saga eksplisit atau penggabungan context.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Domain event → integration event → Outbox → broker** *(dipilih)* | Atomicity state+event; producer/consumer decoupled; rebuild-able | Eventual consistency; infra Outbox/Inbox | Hohpe & Woolf (EIP); Bellemare |
| B. Direct publish ke broker di handler (tanpa Outbox) | Lebih sedikit infra | Dual-write: state commit tapi event hilang saat crash | Hohpe & Woolf (EIP) |
| C. Sinkron call lintas-context (gRPC command) | Strong consistency, simpel dipikirkan | Temporal coupling; cascading failure; melawan otonomi service | Newman (Building Microservices); Nygard (Release It!) |

## Consequences

**Positif**
- Tipe Domain internal **tak pernah** jadi wire-contract — evolusi internal aman ([ADR-0009](0009-contracts-vs-grpc-separation.md)).
- Reporting & Notification cukup subscribe event yang sama tanpa producer tahu ([ADR-0017](0017-eventual-consistency-reporting-notification.md)).
- Event dipersist → projection bisa di-replay/rebuild.

**Trade-off / lebih sulit**
- Harus desain idempotency consumer (Inbox) & kebijakan retry/DLQ ([ADR-0017](0017-eventual-consistency-reporting-notification.md)).
- Debugging alur jadi lintas-proses (butuh correlation-id + tracing).

**Yang harus dijaga**
- Hanya integration event (`*.Contracts`, POCO ber-versi) yang menyeberang broker; domain event tetap in-process.

## Out of scope / deferred

- Saga/orchestration untuk alur multi-langkah yang butuh kompensasi (mis. wave cancel → release stock) belum di-scope — saat ini koreografi event sederhana.
- Pemilihan broker konkret (Azure Service Bus / GCP Pub/Sub) = deploy-time via adapter ([ADR-0002](0002-tri-cloud-hexagonal.md), [ADR-0018](0018-compute-hosting-mixed-paas.md)).

## Amendment — 2026-06-20

> Koreografi & Outbox di atas tetap. Blok ini menambah tiga nuansa; saga **tetap deferred** (hanya RULE batas yang diputuskan dini).

- **Saga boundary RULE** (untuk saga yang masih *deferred*): bila kelak diwujudkan, saga hanya boleh **fully contained di SATU bounded context** (mis. `WaveSaga` internal Outbound), **tak pernah** orchestrator lintas-context. Kompensasi ditulis sebagai compensating event via **Outbox** (single-hop). Memutuskan *di mana* saga boleh hidup tanpa membangun engine-nya — pre-empt god-service. `→ Canon: Richardson (Microservices Patterns), Saga — orchestration vs choreography; Newman (Building Microservices), sagas`.
- **Composite inbox key** `(event_id, handler_type)` — bukan `event_id` saja — agar satu event fan-out di-track independen per consumer handler; mencegah handler pertama memblok sibling (silent loss + DLQ kosong). Nilai utamanya untuk multi-consumer **dalam satu service**.
- **Logical event identity & SemVer bump rule**: nama broker-facing `{module}.{event_snake}.v{N}` decoupled dari tipe CLR (rename kelas ≠ breaking diam-diam); detail di [ADR-0023](0023-event-contract-catalog-asyncapi.md).
