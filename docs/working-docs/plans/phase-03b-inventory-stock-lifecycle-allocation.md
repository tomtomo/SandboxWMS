# Phase 03b — Stock Lifecycle + PutawayTask + FEFO Allocation

**Status:** planned

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
