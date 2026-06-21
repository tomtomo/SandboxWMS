# Phase 03b — Stock Lifecycle + PutawayTask + FEFO Allocation

**Status:** done (2026-06-21)

**Pre-conditions:**
- **03a done:** `GoodsReceipt` full state machine + two-axis discrepancy; `GRConfirmedV1` bawa `receivedLines` (Good/QcHold) / `rejectedLines`; `IObjectStore` ada; semua FF hijau.
- Phase 03 Complete Core Flow (prinsip 3) — instansiasi template, **bukan** building block baru. MasterData belum ada → location (Receiving/Rack/Quarantine area) via **LOCAL SEED**.

**Context refs (WAJIB baca dulu):**
- `docs/tomsandboxwms-overview.md` §B (Stock states + transitions dipicu modul lain, PutawayTask) + §C3 (alokasi FEFO) / §C6 (dispatch removal)
- `docs/adr/0005-event-driven-outbox.md` (koreografi, Inbox idempotency `(event_id, handler_type)`, integration event ber-versi)
- `docs/adr/0028-picking-completed-event.md` (event ke-5 `outbound.picking_completed.v1` — sinyal PickingTask Completed → Stock `Allocated→Picked`)

**Tujuan:** Lengkapi `Stock` jadi **lifecycle penuh** (Quarantine/OnHand/Available/Allocated/Picked) yang ditransisikan oleh event lintas-modul; tambah penyelesaian `PutawayTask` + alokasi **FEFO** saat `WaveReleased`, emit `StockAllocated`, dan habisi stock saat `ShipmentDispatched`.

**Deliverable:**
- `Wms.Inventory.Domain`: `Stock` lifecycle penuh (Quarantine·OnHand·Available·Allocated·Picked) dgn transisi legal; `PutawayTask` completion (OnHand→Available + location change ke Rack).
- `Wms.Inventory.Application`: extend consumer `GRConfirmedV1` — **QcHold→Stock(Quarantine), TANPA PutawayTask**; **Good→Stock(OnHand)+PutawayTask** (per overview §B1).
- Consumer `WaveReleasedV1` (Available→Allocated, strategi **FEFO** sebagai config internal Inventory, emit `StockAllocatedV1` dgn `allocations[]`).
- Consumer `PickingCompletedV1` (Allocated→Picked). Event ke-5 di luar katalog 4 asli — **realisasi sinyal** overview §B (`PickingTask` Completed di Outbound → Stock `Allocated→Picked`), **diputuskan di ADR-0028**; daftarkan di `asyncapi.yaml`.
- Consumer `ShipmentDispatchedV1` (Picked→removed).
- `CompletePutaway` slice (REST) + marker `// TODO-AUTH: Inventory.CompletePutaway`.
- Channel baru ter-register di `asyncapi.yaml` (`outbound.wave_released.v1`, `inventory.stock_allocated.v1`, `outbound.picking_completed.v1`, `outbound.shipment_dispatched.v1`).

**Tasks:**
1. `Stock` lifecycle penuh: transisi legal Quarantine/OnHand→Available (putaway), Available→Allocated, Allocated→Picked, Picked→removed; tolak transisi ilegal di domain.
2. `PutawayTask` completion (`CompletePutaway`: Assigned→Completed, `actualDestinationId`, Stock OnHand→Available + location→Rack); slice REST + `// TODO-AUTH`.
3. Extend consumer `GRConfirmedV1`: QcHold→Stock(Quarantine, **tanpa** PutawayTask), Good→Stock(OnHand)+PutawayTask (Inbox dedup, business-write + inbox-mark satu tx).
4. Consumer `WaveReleasedV1`: per line pilih Stock Available via **FEFO** (expiry terdekat, config internal), mark Allocated(`allocatedToWaveId`); setelah semua line → emit `StockAllocatedV1` (`allocations[]: sku/locationId/batch/qty/stockId`) via Outbox.
5. Tambah `StockAllocatedV1` payload di `Wms.Inventory.Contracts` (logical `inventory.stock_allocated.v1`).
6. Consumer `PickingCompletedV1`: Allocated→Picked (location→staging, `pickingTaskId`) — per ADR-0028; konsumsi via Inbox idempotent.
7. Consumer `ShipmentDispatchedV1`: remove semua Stock state Picked terikat `waveId` (idempotent).
8. Register channel baru di `asyncapi.yaml` agar FF #11 contract-coverage tetap hijau.
9. Domain unit tests: semua transisi `Stock` legal/ilegal (termasuk QcHold tak generate PutawayTask).
10. Integration tests: `WaveReleased`→alokasi FEFO→`StockAllocated` emitted; `PickingCompleted`→Picked; `ShipmentDispatched`→removed; konsumer idempotent (event duplikat → transisi sekali).

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **semua FF incl FF #11 contract-coverage** pass (channel baru ter-cover `asyncapi.yaml`).
- Domain unit tests hijau: transisi `Stock` lengkap legal/ilegal; QcHold→Quarantine tanpa PutawayTask.
- Integration tests hijau: `WaveReleased`→FEFO allocate→`StockAllocated` emitted; `PickingCompleted`→Picked; `ShipmentDispatched`→removed; **konsumer idempotent**.

**Learning objective:** Aggregate lifecycle ditransisikan oleh trigger event-driven berbeda; strategi alokasi FEFO sebagai konfigurasi internal (tak bocor ke kontrak); koordinasi multi-event via koreografi; idempotent state transition (Inbox composite key).

**Handoff notes:** Sisi Inventory core flow lengkap — Stock hidup penuh dari Quarantine/OnHand sampai removed; emit `StockAllocated`; konsumsi `WaveReleased`/`PickingCompleted`/`ShipmentDispatched`. **03c** membangun sisi Outbound (OutboundOrder/Wave/PickingTask) yang **menutup loop**: produce `WaveReleased`, konsumsi `StockAllocated`→PickingTask, produce `PickingCompleted`/`ShipmentDispatched` → E2E core flow utuh.

**Touchpoint cert:** AZ-204 — Service Bus messaging *(pattern)* + Azure DB for PostgreSQL *(EF)* → X. PCD — Pub/Sub *(pattern)* + Cloud SQL → X.

**Out-of-scope:** ⚠ allocation failure (stock Available tak cukup) handling, picking discrepancy (asumsikan `actualQty=qty`) — flag sebagai gap, JANGAN dibangun (out-of-scope global). Event `StockLow`/`StockNearExpiry` biarkan tak ber-consumer (gap ADR-0023).

---

## Completion (2026-06-21)

**Selesai & terverifikasi.** `dotnet build Wms.sln` **0 warning / 0 error**; **127 test hijau** — Inventory.Domain **18** (Stock 14 + PutawayTask 4), Inbound.Domain 48, BuildingBlocks 30, Inventory.Integration **10**, Architecture FF **8** (#1–#7 + #11 contract-coverage), Inbound.Integration 13. Migration `AddStockLifecycleAndPutawayCompletion` apply bersih di Postgres riil (Testcontainers, `InfrastructureMigrationTests`). **Phase 03b = INSTANSIASI template** — Result/pipeline/Outbox/Inbox/contract/SYSTEM/DLQ dipakai apa adanya; nol building-block baru.

**Dibangun (TDD untuk domain, RED→GREEN):**
- **Domain `Stock` lifecycle penuh:** `StockStatus` {Quarantine, OnHand, Available, Allocated, Picked}. Field baru: `LocationId` (berubah sepanjang lifecycle), `Batch`, `Expiry`, `AllocatedToWaveId`, `PickingTaskId`. Factory `CreateOnHand`/`CreateQuarantine`; transisi `Putaway`(OnHand→Available)/`Allocate`(Available→Allocated)/`Pick`(Allocated→Picked) — legal/ilegal via Result (Conflict utk transisi ilegal). Nol throw (FF#7).
- **Domain `PutawayTask` completion:** `PutawayTaskStatus` +Completed; field `SourceLocationId`/`SuggestedDestinationId`/`AssignedTo?`/`ActualDestinationId?`; `Assign` (enriched) + `Complete` (Assigned→Completed). `PutawayTaskErrors` (NotFound/InvalidCompletion/MissingDestination).
- **Contracts (consumer-first):** `Wms.Outbound.Contracts` baru (`WaveReleasedV1`+`WaveLineV1`, `PickingCompletedV1`, `ShipmentDispatchedV1`) + `Wms.Inventory.Contracts` baru (`StockAllocatedV1`+`StockAllocationV1`), masing-masing `const LogicalName`. `asyncapi.yaml` diratifikasi (5 channel, skema presisi + nullable batch). `Wms.Inventory.Api` baru.
- **4 consumer (Inbox idempotent, business+inbox[+outbox] satu tx):** `GoodsReceiptConfirmedConsumer` di-extend (branch Status Good→OnHand+PutawayTask / QcHold→Quarantine tanpa task; konsumsi batch/expiry; ACL unknown-status→loud-fail). `WaveReleasedConsumer` (FEFO internal: muat Available per-sku → antrian per-sku urut expiry → dequeue per line; `Allocate`; emit `StockAllocatedV1` via Outbox). `PickingCompletedConsumer` (Allocated→Picked). `ShipmentDispatchedConsumer` (remove Picked per wave).
- **Slice REST `CompletePutaway`:** Command(IAuditable)+Handler(2 aggregate, 1 tx)+Validator+Endpoint `POST /putaway-tasks/{id}/complete` + `// TODO-AUTH: Inventory.CompletePutaway`. `AddInventoryApplication` (MediatR+behaviors). 
- **Dispatcher** route 4 event (method per-event, helper generik) → Host subscribe 4 wrapped (DLQ source per consumer). Host Inventory jadi **hybrid** (consumer + REST): `AddHttpContextCurrentUser` (REST=operator, consumer-scope=SYSTEM via null HttpContext) + `AddLocalAuditing` + `UseCorrelationId`.
- **Repos** extend: `ListAvailableBySkusAsync`/`GetAsync`/`ListPickedByWaveAsync`/`Remove` (Stock), `GetAsync` (PutawayTask). EF config field baru + index (`status,sku` FEFO; `allocated_to_wave_id` removal). 1 migration additive.
- **FF#2 fix:** honor declared-layers (data-driven) agar modul Contracts-only (Outbound) tak memaksa Load `*.Domain` yang belum lahir.

**Keputusan sadar:**
1. **`Wms.Outbound.Contracts` lahir di 03b (bukan 03c)** — consumer-first: Inventory tak bisa consume tanpa tipe. Producer (Outbound module) menyusul 03c memakai tipe yang sama. asyncapi diupdate "Realisasi: tipe+consumer 03b · emitter 03c".
2. **Model alokasi:** 1 WaveReleased line ↔ 1 batch FEFO (expiry terdekat), dialokasikan **utuh**; `StockAllocationV1.Qty = stock.Quantity` (batch yang direservasi). Partial/split & allocation-failure (insufficient) = out-of-scope (tak dibangun; line tak terpenuhi sekadar tak menghasilkan alokasi — implicit-gap overview §B#2).
3. **FEFO = config internal** di consumer (bukan kontrak); FIFO/LIFO/fixed-bin bisa menggantikan tanpa ubah kontrak.
4. **Lokasi via LOCAL SEED** (`InventoryLocations`: REC-01/QC-A/RACK-A1) — pengganti MasterData sampai 04a; putaway strategy (closest-empty-bin) = saran rak statis.
5. **`AssignedTo` nullable null** — assignment operator belum di-scope (auth → 07a).

**Utang sadar / gap:**
1. Allocation failure (Available tak cukup) — implicit (line tak teralokasi), TANPA partial/reject/notify policy (out-of-scope global).
2. Picking discrepancy (`actualQty<qty`) — out-of-scope; payload `PickingCompletedV1` asumsi `qty` final (revisit ADR-0028).
3. WaveReleased **tak bawa warehouseId** → alokasi match by sku saja (single-warehouse scope; multi-warehouse koordinasi out-of-scope).
4. `StockLow`/`StockNearExpiry` belum lahir (emitted-but-unconsumed gap ADR-0023); `StockQuarantineStale` → 07c.
5. QC release flow (Quarantine→OnHand) out-of-scope global.
6. AuthZ deferred (`// TODO-AUTH: Inventory.CompletePutaway`) → 07a.
7. Aspire dashboard smoke (manual) belum dijalankan.

**Next:** **Phase 03c** — Outbound (OutboundOrder/Wave/PickingTask): produce `WaveReleased`, consume `StockAllocated`→PickingTask, produce `PickingCompleted`/`ShipmentDispatched` → menutup loop E2E core flow. Tipe `Wms.Outbound.Contracts` sudah ada (tinggal di-emit); `Wms.Inventory.Contracts.StockAllocatedV1` siap di-consume.
