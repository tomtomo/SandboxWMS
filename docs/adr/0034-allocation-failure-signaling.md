# ADR-0034: Allocation-failure signaling (stock short) — event eksplisit saat wave allocation tak terpenuhi

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-25
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Inventory `WaveReleasedConsumer` (FEFO allocation) → emit event short saat `remaining > 0`; Outbound consumer tandai `OrderLine` (Allocated/Short); Notification consumer alert. Channel baru di `docs/architecture/asyncapi.yaml` ([ADR-0023](0023-event-contract-catalog-asyncapi.md)), lewat Outbox/Inbox ([ADR-0005](0005-event-driven-outbox.md)), menjaga DB-per-service ([ADR-0010](0010-data-ownership-db-per-service.md)), eventual consistency ([ADR-0017](0017-eventual-consistency-reporting-notification.md))

## Context

Overview §B Implikasi #2 menyatakan **allocation failure** — stock `Available` tak cukup memenuhi `WaveReleased` — **eksplisit out-of-scope** ("partial allocation atau reject seluruh wave? ... out of scope di iterasi ini"). Implementasi saat ini meng-*assume cukup*: `WaveReleasedConsumer` mengalokasi FEFO best-effort, dan line yang stock-nya kurang/nol **di-skip diam-diam** — tak ada partial flag, reject, atau notify. Akibatnya order line yang tak terpenuhi **lenyap tanpa jejak**: tak ada `StockAllocated` untuk line itu → tak ada `PickingTask` → tak pernah dipick/dikirim/di-flag. Ini tidak masuk akal secara bisnis (demand hilang senyap).

Pembanding kanonik: reference app resmi **dotnet/eShop** memisahkan order capture dari availability — order diterima tanpa cek stock sinkron; Catalog memvalidasi stock **async** saat `OrderStatusChangedToAwaitingValidation`, dan saat kurang menerbitkan **`OrderStockRejectedIntegrationEvent`** (order → Cancelled). Sinyal-gagal eksplisit itulah yang hilang di sistem kita. (eShop issue #888 juga menegaskan: cek stock sinkron/tanpa reservasi rawan TOCTOU/overselling — jadi sync ATP-gate bukan obat correctness.)

Keputusan dibutuhkan: bagaimana menangani shortfall **tanpa** melanggar no-sync-cross-context ([ADR-0010](0010-data-ownership-db-per-service.md)) dan tetap loosely-coupled ([ADR-0017](0017-eventual-consistency-reporting-notification.md)).

## Decision

- **Pilihan:** Tambah satu integration event cross-context **`inventory.stock_allocation_failed.v1`** (`StockAllocationFailedV1`, POCO di `Wms.Inventory.Contracts`). Di-emit Inventory dari `WaveReleasedConsumer` saat sebuah line tak teralokasi penuh (`remaining > 0` setelah FEFO), payload inti: `waveId`, `lines[]: { orderId, sku, requestedQty, allocatedQty, shortQty }`. Line yang **sebagian** teralokasi tetap memancarkan `StockAllocated` untuk qty yang berhasil **dan** muncul di event short untuk sisanya. Dikonsumsi **Outbound** (Inbox dedup → tandai `OrderLine.allocationStatus = Short`/Backordered) dan **Notification** (subscribe → alert subscriber/operator). Terdaftar di `asyncapi.yaml`. **TIDAK** ada sync ATP-gate di order-entry; **TIDAK** ada reject seluruh wave.
- **Kenapa:** Memisahkan order capture (demand) dari availability resolution (supply matching), dengan **sinyal-gagal eksplisit** lewat event — persis pola reference eShop (`OrderStockRejected`) yang diterjemahkan ke konteks WMS (allocation di wave, bukan validasi Catalog). State lintas-context **harus** mengalir via versioned event di atas Outbox/Inbox ([ADR-0005](0005-event-driven-outbox.md)) karena tak ada shared store ([ADR-0010](0010-data-ownership-db-per-service.md)); pure-consumer + eventual ([ADR-0017](0017-eventual-consistency-reporting-notification.md)). Bentuknya identik `StockAllocated`/`WaveReleased` (event-carried state transfer, pola [ADR-0030](0030-reporting-event-enrichment.md)). Sync ATP-gate ditolak: temporal coupling + TOCTOU race (eShop #888) — ilusi aman tanpa reservasi atomik. `→ Canon: Newman (Building Microservices), no shared DB — integrate via events; Hohpe & Woolf (EIP), Event Message; Bellemare, event-driven state propagation; Richardson (Microservices Patterns), choreography compensating-signal; reference dotnet/eShop OrderStockRejected.`
- **Trade-off:** Published-language tumbuh 7→8 event cross-context; +2 consumer (Outbound tandai line, Notification alert) + 1 field `OrderLine.allocationStatus`; `StockAllocationV1` ditambah field `OrderId` (additive, non-breaking) agar Outbound bisa atribusi alokasi ke line yang tepat (`MarkLineAllocated`); visibilitas short bersifat **eventual** (telat selama allocation berjalan). Event ini **men-sinyalkan** shortfall, **bukan** mencegah oversell — proteksi konkurensi alokasi tetap di reservation `Available→Allocated` + optimistic token `xmin` ([ADR-0031](0031-optimistic-concurrency-token-xmin.md)).
- **Kapan ditinjau ulang:** Saat **backorder auto re-allocation** (trigger `PutawayCompleted` → coba alokasi ulang line Short), **reservation-at-order**, atau **ATP projection** (read model di Outbound) di-scope — revisit payload/semantik order-completion saat itu.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Event `inventory.stock_allocation_failed.v1` + `OrderLine.allocationStatus`** *(dipilih)* | Sinyal eksplisit; loosely-coupled; pertahankan FEFO partial-fulfillment; sejajar pola eShop `OrderStockRejected`; reusable utk backorder | +1 event published; visibilitas eventual; +2 consumer | Newman; Hohpe & Woolf (EIP); Bellemare; eShop |
| B. Synchronous ATP-gate di order-entry (Outbound query Inventory, reject di muka) | Tolak dini, UX langsung | Temporal coupling lintas-context (langgar [ADR-0010](0010-data-ownership-db-per-service.md)/[ADR-0006](0006-grpc-internal-rest-ui.md)); TOCTOU/oversell tanpa reservasi (eShop #888); eShop sendiri menghindarinya | Newman; eShop #888 |
| C. Reject seluruh wave bila ada satu line short | Sederhana; all-or-nothing | Buang nilai partial-fulfillment FEFO; satu SKU short membatalkan order lain di wave; coarse | — |
| D. Status quo — silent drop | Nol kode | Demand hilang tanpa jejak; tak masuk akal bisnis; tak observable | — |

## Consequences

**Positif**
- Setiap order line punya **nasib eksplisit**: teralokasi (→ picking → dispatch) atau ditandai `Short`/Backordered + ter-notifikasi. Nol silent-drop.
- Tetap loosely-coupled & rebuild-friendly (eventual, event-driven) — selaras [ADR-0017](0017-eventual-consistency-reporting-notification.md); tak menambah coupling sinkron lintas-context.
- Pola reusable untuk backorder auto re-allocation saat `PutawayCompleted` di-scope nanti.
- Memrealisasikan kandidat notifikasi nyata (temuan: Notification mudah di-extend ke event ke-3) — sinyal bisnis yang berguna.

**Trade-off / lebih sulit**
- Katalog event 7→8; contract-coverage FF ([ADR-0023](0023-event-contract-catalog-asyncapi.md)) kini menjaganya juga; Outbound + Notification masing-masing dapat satu consumer idempoten tambahan.
- `OrderLine` bertambah `allocationStatus` (Pending→Allocated|Short) + migrasi skema Outbound.
- Visibilitas short **eventual** — line baru ter-flag setelah allocation berjalan, bukan saat order dibuat.

**Yang harus dijaga**
- Logical identity `inventory.stock_allocation_failed.v1` + SemVer ([ADR-0023](0023-event-contract-catalog-asyncapi.md)); terdaftar di `asyncapi.yaml` (FF#11) & format `{module}.{event}.v{N}`.
- Di-emit dari consumer hanya pada fakta short nyata (`remaining > 0`) ([ADR-0026](0026-tactical-ddd-conventions.md)); Inbox composite key `(event_id, handler_type)` di tiap consumer baru ([ADR-0005](0005-event-driven-outbox.md)).
- Konsistensi qty: untuk line partial, `allocatedQty (StockAllocated) + shortQty (failed) == requestedQty`.

## Out of scope / deferred

- **Backorder auto re-allocation** (trigger `PutawayCompleted` → alokasi ulang line `Short`) — pola disiapkan, eksekusi deferred (follow-up).
- **Reservation-at-order** & **ATP projection / customer-facing availability** — tetap out of scope (order capture ≠ supply matching).
- **Semantik order-completion saat masih ada line Short** (apakah order `Closed` parsial, atau tetap terbuka menunggu backorder) — diputuskan saat backorder lifecycle di-scope.
- **Wave reschedule/cancel** (release `Allocated→Available`) — tetap out of scope (overview §C #2).

## Amendment — 2026-06-25 (via ADR-0035)

- **Rename event:** `StockAllocationFailedV1` → **`StockAllocationShortfallV1`** (`StockAllocationFailedLineV1` → `StockAllocationShortfallLineV1`); LogicalName `inventory.stock_allocation_failed.v1` → **`inventory.stock_allocation_shortfall.v1`**. Alasan: nama "Shortfall" (selisih kurang) akurat untuk line nol **dan** parsial; "Failed" terkesan total, "Partial" mengecualikan line yang dapat 0. Selaras vocab `OrderLine.AllocationStatus.Short`/`ShortQty`. Prosa keputusan asli tak berubah (kontrak sama, hanya nama). Non-breaking: belum ter-commit/ter-deploy saat rename.
- **Wave nol-seluruhnya kini punya jalan keluar** ([ADR-0035](0035-auto-cancel-unfulfillable-wave.md)): `StockAllocated` kosong → auto-cancel wave + return order ke backlog. Event shortfall TETAP menyala untuk wave nol (tiap line allocated 0) → sekaligus jadi notifikasi cancel (nol event broker baru).
