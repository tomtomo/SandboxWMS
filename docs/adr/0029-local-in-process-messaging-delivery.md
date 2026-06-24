# ADR-0029: Local messaging delivery — in-proc; walking-skeleton E2E via test 1-proses; cross-process di-defer ke broker cloud

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-21
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Adapter Local `InMemoryMessagePublisher` (`Platform.Local`), orkestrasi AppHost ([ADR-0008](0008-aspire-distributed-local.md)), harness integration test; titik konsumsi lintas-host pertama (Phase 01c Inbound→Inventory)

## Context

Phase 01c menghidupkan konsumen lintas-context **pertama**: Inbound emit `GRConfirmed` via Outbox → Inventory consume. Di sinilah asumsi rail bertemu kenyataan adapter Local. Rail Local `InMemoryMessagePublisher` ([ADR-0005](0005-event-driven-outbox.md), dibangun 01b) mem-fan-out **in-process** (`ConcurrentDictionary` subscriber). Sementara [ADR-0008](0008-aspire-distributed-local.md) menjalankan tiap modul sebagai **host terpisah** di AppHost = **proses terpisah**, dan [ADR-0010](0010-data-ownership-db-per-service.md) (DB-per-service) melarang bus lewat tabel bersama.

Akibatnya `OutboxDispatcher` di host Inbound mem-publish ke publisher in-proc-nya **sendiri**; subscriber Inventory yang hidup di proses lain **tak menerimanya**. Walking skeleton (Cockburn) menuntut bukti E2E chain — pertanyaannya: **di mana** chain itu dibuktikan, dan apakah transport Local lintas-proses perlu dibangun sekarang.

## Decision

- **Pilihan:** **Local delivery tetap in-process; walking-skeleton E2E dibuktikan lewat integration test 1-proses; delivery lintas-proses di-defer ke adapter broker cloud.** Dua host Inbound/Inventory tetap di-declare di AppHost ([ADR-0008](0008-aspire-distributed-local.md)) — `dotnet run AppHost` *smoke* = kedua service UP + REST live. Bukti choreography E2E (publish→dispatch→consume→Inbox idempotent) ada di integration test yang meng-host producer + consumer dalam **satu proses** (pola `OutboxInboxRailTests`). Titik `Subscribe` konsumen tetap di-wire di host (idle saat runtime AppHost 2-proses) sebagai *consumer endpoint* yang nanti disambungkan adapter broker.
- **Kenapa:** Lintas-proses **memang** tugas broker (Azure Service Bus / GCP Pub/Sub) — itulah guna port `IMessagePublisher` + seam Hexagonal. Membangun broker Local nyata sekarang = kompleksitas yang belum di-*earn* untuk skeleton; meng-co-host 2 modul jadi 1 proses = membuang topologi per-host yang jadi inti blueprint. Test 1-proses sudah meng-exercise **seluruh** mekanisme rail (Outbox→publisher→subscriber→Inbox) secara deterministik — itu *gate* DoD yang keras. `→ Canon: Cockburn (Hexagonal), adapter Local stand-in vs adapter cloud sebagai impl port sebenarnya; Cockburn (Walking Skeleton), E2E tertipis yang menautkan komponen; Newman (Building Microservices), dev-experience & "jangan bangun infra sebelum perlu"; Ford et al. (Evolutionary Architecture), earn kompleksitas via pemicu`.
- **Trade-off:** Bagian DoD "smoke AppHost → POST GR → Inventory state terbentuk" **tak terpenuhi lintas-proses** di Local — efek Inventory-dari-Inbound baru terbukti di test, bukan lewat F5 dua-proses. Seam broker baru teruji sungguhan di Phase 05/06 (risiko integrasi bergeser ke sana).
- **Kapan ditinjau ulang:** Saat butuh dev-loop/demo lintas-proses lokal yang nyata sebelum cloud (mis. WebUI lokal yang bergantung pada efek Inventory) → tambah adapter transport Local lintas-proses (Redis pub/sub via Aspire, atau Postgres `LISTEN/NOTIFY` pada store khusus) **tanpa menyentuh core** (cukup `Platform.Local`).

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. In-proc + E2E via test 1-proses; cross-process defer ke broker cloud** *(dipilih)* | Nol infra baru; gate DoD (test) lulus penuh; hormati topologi 2-host & "Local in-proc" (01b); seam broker teruji di tempatnya (cloud) | Smoke AppHost lintas-proses tak jalan; risiko integrasi broker bergeser ke P05/06 | Cockburn (Hexagonal); Newman; Ford et al. |
| B. Composite Local host (Inbound+Inventory 1 proses) | Smoke AppHost full jalan via rail nyata in-proc | Menyimpang dari host-per-modul ([ADR-0008](0008-aspire-distributed-local.md)); topologi Local ≠ Cloud | Newman (in-proc local, eShop) |
| C. Broker Local nyata (Redis / `LISTEN-NOTIFY`) | Fidelity = topologi cloud; 2 host genuinely tukar event lokal | Infra + adapter baru belum di-*earn*; lawan "Local = in-proc stand-in" | Hohpe & Woolf (EIP) |

## Consequences

**Positif**
- Walking skeleton selesai tanpa menambah infra; deliverable pertama tetap *thin*.
- Topologi per-host ([ADR-0008](0008-aspire-distributed-local.md)) + DB-per-service ([ADR-0010](0010-data-ownership-db-per-service.md)) utuh; tak ada bus tabel-bersama.
- Subscribe-point konsumen jadi kontrak yang jelas untuk adapter broker cloud (Phase 05/06).

**Trade-off / lebih sulit**
- Delivery lintas-proses Local tak ter-cover otomatis — dicatat sebagai **gap** sampai broker cloud hadir; debugging choreography lokal terbatas ke test.

**Yang harus dijaga**
- Bukti E2E + idempotency hidup di integration test 1-proses (jangan andalkan smoke AppHost untuk klaim chain).
- Saat adapter broker cloud (P05/06) — atau transport Local lintas-proses bila ditinjau ulang — hadir, **sambungkan** subscribe-point yang sudah di-wire, bukan menulis ulang konsumen.

## Out of scope / deferred

- Transport Local lintas-proses (Redis / Pub-Sub emulator / `LISTEN-NOTIFY`) — di-defer (lihat "Kapan ditinjau ulang").
- Adapter broker konkret Azure Service Bus / GCP Pub/Sub = Phase 05/06 ([ADR-0002](0002-tri-cloud-hexagonal.md), [ADR-0018](0018-compute-hosting-mixed-paas.md)).
- Cross-broker trace-context saat hop broker nyata = [ADR-0024](0024-cross-broker-trace-context-propagation.md) (baru relevan ketika broker lintas-proses aktif).

---

## Amandemen — RabbitMQ sebagai broker Local lintas-proses (2026-06-25)

**Pemicu (sesuai "Kapan ditinjau ulang"):** Pematangan `Wms.WebUI` menuntut **happy-path E2E lewat app yang berjalan** — alur bisnis penuh (GR Confirmed → Inventory buat Stock+Putaway → Wave → alokasi → PickingTask → dispatch → stock keluar) harus mengalir antar-modul saat UI dipakai, bukan cuma terbukti di test 1-proses. Inilah skenario "WebUI lokal yang bergantung pada efek Inventory" yang diantisipasi di atas. Keputusan Tom (2026-06-25): aktifkan delivery lintas-proses Local sekarang.

**Keputusan:** tambah **RabbitMQ** sebagai adapter transport Local lintas-proses, **tanpa menyentuh core** (sesuai janji "cukup `Platform.Local`"):
- AppHost ([ADR-0008](0008-aspire-distributed-local.md)) men-declare resource `AddRabbitMQ("rabbitmq").WithManagementPlugin()` (container); connection string di-inject ke 5 host event-driven (inbound/inventory/outbound/reporting/notification) via `WithReference`.
- `Platform.Local` dapat adapter baru: `RabbitMqMessagePublisher` (port `IMessagePublisher` → publish ke topic exchange `wms.events`, routing key = `LogicalName`, publisher-confirm) + `RabbitMqConsumer`/`RabbitMqConsumerHostedService` (queue durable per modul, bind `#`, `AsyncEventingBasicConsumer`). **Subscribe-point yang dulu IDLE kini tersambung ke adapter ini.**
- Port baru **`IMessageSubscriber`** (`BuildingBlocks.Application.Messaging`) menyeragamkan blok subscribe host: di-implement `InMemoryMessagePublisher` (in-proc) DAN `RabbitMqConsumer` (broker). Pemilihan adapter = ada/tidaknya `ConnectionStrings:rabbitmq` (Aspire → RabbitMQ; test/single-proses → in-proc).
- Rail **Outbox → broker → Inbox** ([ADR-0005](0005-event-driven-outbox.md)), `ConsumerDeadLetterPipeline` (retry→DLQ), dan dispatcher per-modul **TAK berubah** — hanya transport publisher/subscriber yang di-swap (bukti kekuatan seam Hexagonal, [ADR-0002](0002-tri-cloud-hexagonal.md)). `→ Canon: Hohpe & Woolf (EIP) Guaranteed Delivery + Dead Letter; Kleppmann (DDIA) at-least-once + idempotent receiver.`

**Yang dipertahankan:** `InMemoryMessagePublisher` **tetap ada** — integration test choreography 1-proses (`WalkingSkeletonChainTests`, `CoreFlowE2ETests`) menyusun banyak modul dalam satu proses berbagi satu publisher; itu tetap gate DoD yang keras. RabbitMQ tak menggantikan bukti test, ia menambah jalur live.

**Trade-off:** Local kini butuh **container RabbitMQ** (Docker) untuk chain multi-proses penuh; test tetap in-proc (nol broker). RabbitMQ adalah **broker dev/Local** — **bukan** target cloud: adapter Azure Service Bus / GCP Pub/Sub tetap Phase 05/06. Nilainya: jalur Local kini meng-exercise bentuk-broker nyata (async, at-least-once, DLQ, ordering per-queue) → mengurangi risiko integrasi yang dulu sepenuhnya digeser ke P05/06.

**Catatan paket (lockstep net8, [ADR-0007](0007-net8-lts-baseline.md)):** host net8 pakai `RabbitMQ.Client` **6.x** langsung (API sync `IModel`) via `Platform.Local` — **sengaja BUKAN** `Aspire.RabbitMQ.Client.v6`, yang menarik `Microsoft.Extensions.* 10.0.8` transitif dan bentrok lockstep net8 lewat transitive pinning. `Aspire.Hosting.RabbitMQ` (butuh `RabbitMQ.Client` 7.x) hanya di AppHost (net10, tak bicara AMQP) via `VersionOverride` — proses terpisah, versi beda tak bentrok.

**Konsekuensi lanjutan:** [ADR-0024](0024-cross-broker-trace-context-propagation.md) (cross-broker trace-context) kini **relevan** — hop broker lintas-proses aktif. `MessageEnvelope.Traceparent/Tracestate` masih `null` (TODO-07B di `OutboxExtensions.AddToOutbox`); pengisian dari `Activity.Current` menyusul saat Phase 07b agar trace utuh menembus hop RabbitMQ. Tidak memblok fungsi delivery.

**Status amandemen:** Accepted (2026-06-25). Tak men-*supersede* keputusan asli (in-proc + test 1-proses tetap berlaku untuk test); menambah jalur broker Local lintas-proses yang dulu di-defer.
