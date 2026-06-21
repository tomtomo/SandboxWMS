# Phase 03c — Outbound: OutboundOrder + Wave + PickingTask → Core Flow E2E

**Status:** planned

**Pre-conditions:**
- **03b done:** Stock lifecycle penuh + FEFO allocation; Inventory emit `StockAllocatedV1`, konsumsi `WaveReleasedV1`/`PickingCompletedV1`/`ShipmentDispatchedV1`; channel ter-register di `asyncapi.yaml`; semua FF hijau.
- **Capstone Phase 03 Complete Core Flow** (prinsip 3) — akhir sub-phase ini = **core flow Inbound→Inventory→Outbound utuh s/d ShipmentDispatched, semua LOCAL via Aspire**. Instansiasi template, bukan building block baru. MasterData belum ada → `orderLines` snapshot via **LOCAL SEED** (ADR-0014).

**Context refs (WAJIB baca dulu):**
- `docs/tomsandboxwms-overview.md` §C (OutboundOrder/Wave/PickingTask flow C1–C6)
- `docs/adr/0005-event-driven-outbox.md` (**saga boundary RULE** — bila ada saga, fully contained di SATU context = Outbound saja; kompensasi via Outbox single-hop)
- `docs/adr/0028-picking-completed-event.md` (event `outbound.picking_completed.v1` yang di-emit Outbound saat PickingTask Completed)

**Tujuan:** Bangun sisi Outbound penuh (`OutboundOrder`/`Wave`/`PickingTask`) yang **menutup core event chain** — produce `WaveReleased`, konsumsi `StockAllocated`→PickingTask, produce `PickingCompleted`/`ShipmentDispatched` — lalu buktikan **E2E core flow** end-to-end lokal.

**Deliverable:**
- `Wms.Outbound.Domain`: `OutboundOrder` (New·InProgress·Closed), `Wave` (Active·Ready·Dispatched), `PickingTask` (Assigned·Completed).
- `Wms.Outbound.Application` slices: terima `OutboundOrder` (snapshot `orderLines` via seed master data); `CreateWave` (orders→InProgress, emit `WaveReleasedV1`); consume `StockAllocatedV1` → create `PickingTask` per allocation; `CompletePicking` (jalur Allocated→Picked: emit `PickingCompletedV1`); `Wave`→Ready saat semua `PickingTask` Completed; `DispatchWave` (emit `ShipmentDispatchedV1`, orders→Closed).
- `Wms.Outbound.Contracts`: `WaveReleasedV1` (`outbound.wave_released.v1`), `PickingCompletedV1` (`outbound.picking_completed.v1`), `ShipmentDispatchedV1` (`outbound.shipment_dispatched.v1`).
- REST endpoint + marker `// TODO-AUTH` (Outbound.CreateWave / Outbound.CompletePicking / Outbound.DispatchWave).
- Host Outbound ter-declare di `Wms.AppHost`.

**Tasks:**
1. `OutboundOrder` aggregate (terima order eksternal → New; snapshot `orderLines` `sku`/`qty`/`uom` via seed, ADR-0014).
2. `Wave` aggregate + slice `CreateWave`: masukkan orders (New→InProgress), Wave→Active, emit `WaveReleasedV1` (`lines[]: orderId/sku/qty`) via Outbox.
3. Tambah `WaveReleasedV1`/`PickingCompletedV1`/`ShipmentDispatchedV1` di `Wms.Outbound.Contracts` + logical name; konfirmasi sudah ter-register `asyncapi.yaml` (FF #11).
4. Consumer `StockAllocatedV1` (Inbox dedup): per entry `allocations[]` create `PickingTask`(Assigned), isi `Wave.pickingTaskIds`.
5. Slice `CompletePicking`: `PickingTask` Assigned→Completed (`actualQty=qty`, `stagingLocationId`), emit `PickingCompletedV1` via Outbox; REST + `// TODO-AUTH`.
6. Transisi `Wave`→Ready saat **semua** `PickingTask` Completed (aturan agregasi di domain).
7. Slice `DispatchWave`: Wave Ready→Dispatched, orders InProgress→Closed, emit `ShipmentDispatchedV1`; REST + `// TODO-AUTH`.
8. Declare Outbound host + dependency di `Wms.AppHost`.
9. Domain unit tests: transisi `OutboundOrder`/`Wave`/`PickingTask` legal/ilegal; Wave→Ready hanya saat semua PickingTask Completed.
10. **Full core-flow E2E integration test** + Aspire smoke via REST. ⚠ bila task list >10 acceptable sebagai capstone — tetap ketat lewat building block, jangan reinvent.

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **semua FF pass**.
- **Full core-flow E2E integration test** hijau: `OutboundOrder` → `Wave` → `WaveReleased` → `StockAllocated` → `PickingTask` → `PickingCompleted` → `ShipmentDispatched` → **Stock removed + OutboundOrder Closed**.
- Domain unit tests hijau (transisi tiga aggregate; Wave→Ready gate).
- **Aspire smoke**: `dotnet run --project src/AppHost/Wms.AppHost` → drive order→wave→pick→dispatch via REST → Stock keluar + order Closed. **Core flow lokal lengkap.**

**Learning objective:** Koordinasi aggregate via koreografi end-to-end; Wave grouping (efisiensi picking/dispatch); **saga-boundary RULE** (contained-in-one-context, kompensasi single-hop Outbox — ADR-0005); menutup core event chain Inbound→Inventory→Outbound.

**Handoff notes:** **CORE FLOW COMPLETE secara lokal** — Inbound→Inventory→Outbound s/d ShipmentDispatched hidup via Aspire (Outbox/Inbox, koreografi). Domain core penuh; master data masih seed snapshot. **Phase 04** menambah supporting services: **04a MasterData menggantikan seed snapshot** (read-API gRPC + cache-aside), 04b Auth, 04c Reporting, 04d Notification, 04e WebUI+Gateway.

**Touchpoint cert:** AZ-204 — Service Bus messaging *(pattern)* + Azure DB for PostgreSQL *(EF)* → X. PCD — Pub/Sub *(pattern)* + Cloud SQL → X.

**Out-of-scope:** ⚠ wave reschedule/cancel (kalau Wave dibatalkan, release Allocated→Available), picking discrepancy (`actualQty<qty`) — flag sebagai gap, JANGAN dibangun (out-of-scope global; saga cancel tetap deferred per ADR-0005).
