# ADR-0030: Event enrichment & Inventory stock-level events untuk Reporting projections (event-carried state transfer)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-22
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Reporting (pure consumer, [ADR-0017](0017-eventual-consistency-reporting-notification.md)) membangun projection §F dari integration event lintas-context; producer Inbound/Outbound/Inventory; katalog `docs/architecture/asyncapi.yaml` ([ADR-0023](0023-event-contract-catalog-asyncapi.md)), Outbox/Inbox ([ADR-0005](0005-event-driven-outbox.md)), DB-per-service ([ADR-0010](0010-data-ownership-db-per-service.md))

## Context

Phase 04c membangun Reporting — read-side CQRS yang mem-build empat projection (overview §F): `StockOnHandView`, `ReceivingSummary`, `DispatchSummary`, `OperatorActivity`. Saat sequencing implementasi, muncul **gap**: integration event yang dibangun Phase 03a–03c adalah **event-notification tipis** (mis. `ShipmentDispatchedV1` = `waveId` saja), sengaja minimal karena konsumen aslinya (**Inventory**) bisa **menderivasi** sisanya dari **store-nya sendiri**. Reporting **tak bisa** — ia service terpisah (**DB-per-service**, [ADR-0010](0010-data-ownership-db-per-service.md)) yang **hanya melihat payload event**; [ADR-0017](0017-eventual-consistency-reporting-notification.md) menolak sync-query ke core. Pemetaan gap (terverifikasi dari `*.Contracts` + `asyncapi.yaml`):

| Projection §F | Butuh | Event sekarang | Gap |
|---|---|---|---|
| `StockOnHandView` | `+qty` (warehouse, sku, batch) saat receive; **`−qty` saat keluar gudang** | `inbound.gr_confirmed.v1` ✅ / `outbound.shipment_dispatched.v1` | decrement: event cuma `waveId` — tanpa warehouse/sku/qty |
| `ReceivingSummary` | qty received + discrepancy rate **per supplier** | `inbound.gr_confirmed.v1` | tanpa `supplierId` |
| `DispatchSummary` | **volume (unit)** + throughput | `outbound.shipment_dispatched.v1` | tanpa kuantitas |
| `OperatorActivity` | **putaway count** + **pick count per operator** | (tak ada) + `outbound.picking_completed.v1` | event putaway-completed **tak ada**; picking-completed tanpa operator |

Tegangan inti: **event-notification** (sinyal "sesuatu terjadi", konsumen tanya balik) vs **event-carried state transfer** (event membawa state yang dibutuhkan konsumen, nol query balik). Notifikasi tipis cukup untuk Inventory (punya store-nya), tapi Reporting butuh state ter-bawa. Sumber kebenaran payload tetap `asyncapi.yaml` + SemVer ([ADR-0023](0023-event-contract-catalog-asyncapi.md)).

## Decision

- **Pilihan:** Adopsi **event-carried state transfer** untuk kebutuhan read-side, dalam dua bentuk minimal — **enrich field non-breaking** di event yang sudah ada, dan **dua integration event baru yang di-emit oleh pemilik datanya** (Inventory). Bukan enrich event yang pemiliknya tak punya datanya, bukan sync-query.

  1. **`inbound.gr_confirmed.v1` + `supplierId`** (nullable → tetap **v1**, [ADR-0023](0023-event-contract-catalog-asyncapi.md) SemVer). `supplierId` = **data domain** milik `GoodsReceipt` → di-bawa **domain event** `GoodsReceiptConfirmed`, diterjemahkan handler. Memenuhi `ReceivingSummary` per-supplier.
  2. **`outbound.picking_completed.v1` + `operatorId`** (nullable → tetap **v1**). `operatorId` = **concern aktor/audit**, *bukan* data domain → **TIDAK** masuk domain event `PickingCompleted`; di-source handler dari `ICurrentUser` ([ADR-0027](0027-system-actor-convention.md)) saat translate. Memenuhi `OperatorActivity` pick-count.
  3. **Event baru `inventory.putaway_completed.v1`** (`PutawayCompletedV1`, `Wms.Inventory.Contracts`). Di-emit Inventory saat `CompletePutaway` (PutawayTask Assigned→Completed): `putawayTaskId`, `stockId`, `sku`, `warehouseId`, `operatorId` (dari `ICurrentUser`). Memenuhi `OperatorActivity` putaway-count.
  4. **Event baru `inventory.stock_removed.v1`** (`StockRemovedV1`, `Wms.Inventory.Contracts`). Di-emit Inventory saat ia meng-consume `ShipmentDispatched` dan **menghapus Stock Picked** (overview §B `Picked→removed`): `waveId` + `lines[]{ warehouseId, sku, batch, qty }`. Memenuhi `StockOnHandView` decrement **dan** `DispatchSummary` (count + volume).

- **Kenapa:** Perubahan state lintas-context harus mengalir via versioned event dari **pemilik datanya** — Inventory memiliki `Stock` (punya `WarehouseId`/`qty` saat removal), Outbound **tidak** (cuma `waveId`/`shipTo`). Mengemit decrement dari Inventory **identik bentuknya** dengan presedimen [ADR-0028](0028-picking-completed-event.md) ("kanal yang hilang muncul saat sequencing roadmap, bukan desain baru") dan menjaga [ADR-0010](0010-data-ownership-db-per-service.md) tanpa memaksa warehouse merembes ke Outbound. Field-enrichment yang **datanya dimiliki emitter** (supplier di Inbound, operator dari aktor) cukup ditambah non-breaking. `→ Canon: Fowler (event-carried state transfer); Hohpe & Woolf (EIP), Document Message vs Event Message; Kleppmann (DDIA), derived data & change events; Newman (Building Microservices), no shared DB — integrate via events; Bellemare, event-driven state propagation.`

- **Trade-off:** Published-language surface tumbuh **5→7 event** + 2 field nullable; Inventory kini producer **3 event** (StockAllocated + putaway_completed + stock_removed). Tiap event ter-bawa-state lebih gemuk = lebih banyak yang harus stabil & ter-version. **Divergence §F-literal**: `DispatchSummary` & `StockOnHandView`-decrement di-feed `inventory.stock_removed.v1` (bukan `outbound.shipment_dispatched.v1` apa adanya) — intent §F dipenuhi, *sumber* event-nya yang ownership-correct. **`operatorId` = SYSTEM** sampai authZ aktif ([ADR-0012](0012-deferred-authorization-enforcement.md), 07a): mekanisme projection lengkap, atribusi per-operator nyata menyala saat auth wired.

- **Kapan ditinjau ulang:** Bila volume event-carried-state membengkak (payload besar/sering) → pertimbangkan claim-check (EIP) atau kembali ke notifikasi + read-API khusus. Bila picking/putaway discrepancy di-scope → payload `qty` mungkin jadi sumbu aktual (ikuti [ADR-0013](0013-two-axis-discrepancy-inbound.md)).

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Event-carried state transfer: enrich field (emitter punya datanya) + 2 event baru di-emit pemilik (Inventory)** *(dipilih)* | Ownership-correct ([ADR-0010](0010-data-ownership-db-per-service.md)); rebuild-able; pola = [ADR-0028](0028-picking-completed-event.md); nol sync-coupling | +2 event & 2 field di published language; divergence §F-literal (sumber decrement) | Fowler; Hohpe & Woolf (EIP); Kleppmann (DDIA); Newman |
| B. Enrich `outbound.shipment_dispatched.v1` dgn lines+warehouse | Patuh §F-literal (sumber = dispatch event) | Outbound **tak punya** warehouse → harus merembeskan warehouse via `StockAllocatedV1` ke Outbound lalu echo balik; menyebar data Inventory ke Outbound; langgar ownership | Newman; [ADR-0010](0010-data-ownership-db-per-service.md) |
| C. Degrade projection ke event tipis sekarang | Nol perubahan producer | `StockOnHandView` cuma increment (menyesatkan); tak ada per-supplier/operator; langgar §F + disiplin QA | — |
| D. Reporting sync-query core saat projeksi | Selalu fresh | Langgar [ADR-0017](0017-eventual-consistency-reporting-notification.md) (sudah menolak sync-query); rusak rebuild-from-events | Kleppmann (DDIA) |

## Consequences

**Positif**
- Empat projection §F bisa di-build **faithful** dari event, **rebuild-able** (replay) — derived data ([ADR-0017](0017-eventual-consistency-reporting-notification.md)).
- Tiap event baru di-emit **pemilik datanya** → boundary DB-per-service utuh; Reporting nambah konsumen tanpa menyentuh write-model core ([ADR-0005](0005-event-driven-outbox.md)).
- `inventory.stock_removed.v1` melayani **dua** projection (decrement + dispatch) dari satu event ownership-correct — tak ada korelasi lintas-event di consumer.

**Trade-off / lebih sulit**
- Katalog event 5→7; **FF #11** ([ADR-0023](0023-event-contract-catalog-asyncapi.md)) kini menjaga `inventory.putaway_completed.v1` + `inventory.stock_removed.v1` (wajib punya channel di `asyncapi.yaml`).
- `OperatorActivity` ter-atribusi **SYSTEM** sampai 07a — projection benar, data per-operator menunggu authZ.

**Yang harus dijaga**
- Field nullable-add = **non-breaking, tetap vN** ([ADR-0023](0023-event-contract-catalog-asyncapi.md)); konsumen lama (Inventory consume `picking_completed`) abaikan field baru.
- Logical identity `inventory.putaway_completed.v1` / `inventory.stock_removed.v1` + SemVer; di-emit dari fakta sukses dalam SATU transaksi dgn Inbox-mark/state ([ADR-0005](0005-event-driven-outbox.md)/[ADR-0026](0026-tactical-ddd-conventions.md)); terdaftar di `asyncapi.yaml`.
- `operatorId` di-source `ICurrentUser` (SYSTEM saat origin-mesin, [ADR-0027](0027-system-actor-convention.md)) — **bukan** masuk domain event (domain nol-aktor).

## Out of scope / deferred

- Migrasi projection store ke NoSQL/Cosmos — deferred ([ADR-0017](0017-eventual-consistency-reporting-notification.md)).
- Atribusi operator **nyata** (non-SYSTEM) — menyala saat authZ wire-up ([ADR-0012](0012-deferred-authorization-enforcement.md), 07a).
- Enrich `outbound.shipment_dispatched.v1` — **tidak**; decrement/dispatch di-derive `inventory.stock_removed.v1` (ownership). `shipment_dispatched` tetap dipakai consumer Inventory apa adanya.
- Claim-check / event store dedicated jangka-panjang — pakai Outbox retention ([ADR-0017](0017-eventual-consistency-reporting-notification.md)).
