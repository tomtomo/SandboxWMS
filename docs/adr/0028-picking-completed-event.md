# ADR-0028: Picking-completed integration event (Outbound→Inventory) untuk transisi Stock Allocated→Picked

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-21
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Outbound `PickingTask` (Assigned→Completed) → emit; Inventory `Stock` consumer (Allocated→Picked, set `pickingTaskId` + staging location). Channel baru di `docs/architecture/asyncapi.yaml` ([ADR-0023](0023-event-contract-catalog-asyncapi.md)), lewat Outbox/Inbox ([ADR-0005](0005-event-driven-outbox.md)), menjaga DB-per-service ([ADR-0010](0010-data-ownership-db-per-service.md))

## Context

Overview §B menyatakan transisi `Stock: Allocated → Picked` **dipicu `PickingTask: Assigned → Completed` di Outbound**, dan state `Stock.Picked` menyimpan `pickingTaskId` + staging location (data hasil operasi picking). Tapi katalog cross-context event yang eksplisit hanya **empat** — `GRConfirmed`, `WaveReleased`, `StockAllocated`, `ShipmentDispatched` — tak ada kanal yang membawa sinyal picking-completed. Karena `Stock` milik Inventory dan `PickingTask` milik Outbound, DB-per-service ([ADR-0010](0010-data-ownership-db-per-service.md)) melarang Outbound menulis store Inventory. Jadi trigger sudah didokumentasikan tapi **kanalnya hilang** — gap yang muncul saat sequencing roadmap, bukan keputusan desain baru.

## Decision

- **Pilihan:** Tambah satu integration event cross-context **`outbound.picking_completed.v1`** (`PickingCompletedV1`, POCO di `Wms.Outbound.Contracts`). Di-emit Outbound saat `PickingTask` Assigned→Completed (payload inti: `waveId`, `pickingTaskId`, `stockId`, `sku`, `batch`, `qty`, `stagingLocationId`). Dikonsumsi Inventory (Inbox dedup) → transisi `Stock` Allocated→Picked + set `pickingTaskId`/staging. Terdaftar di `asyncapi.yaml`.
- **Kenapa:** Perubahan state lintas-context **harus** mengalir via versioned integration event di atas Outbox/Inbox ([ADR-0005](0005-event-driven-outbox.md)) karena tak ada shared store ([ADR-0010](0010-data-ownership-db-per-service.md)). Transisi ini sudah diwajibkan overview §B; keputusan ini hanya **merealisasikan kanal yang hilang**, identik bentuknya dengan `WaveReleased`/`StockAllocated`. `→ Canon: Newman (Building Microservices), no shared DB — integrate via events; Hohpe & Woolf (EIP), Event/Document Message; Bellemare, event-driven state propagation`.
- **Trade-off:** Published-language surface tumbuh 4→5 event cross-context; satu kanal + consumer idempoten + entri contract-coverage FF tambahan untuk dijaga.
- **Kapan ditinjau ulang:** Bila **picking discrepancy** (`actualQty < qty`) di-scope — payload mungkin perlu kuantitas aktual dan jadi sumbu discrepancy mirip Inbound ([ADR-0013](0013-two-axis-discrepancy-inbound.md)); revisit saat itu.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Dedicated event `outbound.picking_completed.v1`** *(dipilih)* | EDA-consistent; granular per PickingTask; bentuk sama dgn event existing; Stock.Picked terrealisasi penuh | +1 event di published language | Newman; Hohpe & Woolf (EIP); Bellemare |
| B. Tanpa event — `ShipmentDispatched` handler hapus Stock `Allocated`/`Picked` per `waveId` | Nol event baru | Kontradiksi overview §B; `Stock.Picked` (`pickingTaskId`/staging) tak pernah keisi → state kehilangan makna domain | — |
| C. Synchronous gRPC Outbound→Inventory set Picked | Langsung | Langgar "yang bisa async tetap event" ([ADR-0006](0006-grpc-internal-rest-ui.md)); temporal coupling; rusak koreografi | Newman |

## Consequences

**Positif**
- State `Stock.Picked` terrealisasi penuh (`pickingTaskId` + staging) sesuai overview §B; agregasi `Wave→Ready` (semua PickingTask Completed) dan removal `ShipmentDispatched` (`Picked→removed`) jadi konsisten.
- Pola reusable saat picking discrepancy di-scope nanti (mengikuti [ADR-0013](0013-two-axis-discrepancy-inbound.md)).

**Trade-off / lebih sulit**
- Katalog event 4→5; contract-coverage FF ([ADR-0023](0023-event-contract-catalog-asyncapi.md)) kini menjaganya juga; Inventory dapat satu consumer idempoten tambahan.

**Yang harus dijaga**
- Logical identity `outbound.picking_completed.v1` + SemVer ([ADR-0023](0023-event-contract-catalog-asyncapi.md)); di-emit dari dalam aggregate hanya pada fakta sukses ([ADR-0026](0026-tactical-ddd-conventions.md)); Inbox composite key `(event_id, handler_type)` ([ADR-0005](0005-event-driven-outbox.md)); terdaftar di `asyncapi.yaml`.

## Out of scope / deferred

- Picking discrepancy (`actualQty < qty`) — global out-of-scope; saat di-scope ikuti pola two-axis Inbound ([ADR-0013](0013-two-axis-discrepancy-inbound.md)) dan revisit payload.
- Wave reschedule/cancel (release `Allocated→Available`) — out of scope.
- Update narasi event-table di `tomsandboxwms-overview.md` §C bila diinginkan — overview §B sudah mendokumentasikan trigger-nya; surface event otoritatif tetap `asyncapi.yaml` ([ADR-0023](0023-event-contract-catalog-asyncapi.md)).
