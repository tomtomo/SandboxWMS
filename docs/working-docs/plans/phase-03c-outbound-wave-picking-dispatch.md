# Phase 03c — Outbound: OutboundOrder + Wave + PickingTask → Core Flow E2E

**Status:** done (2026-06-22)

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

**Handoff notes (state akhir 2026-06-22):** **CORE FLOW COMPLETE secara lokal.** `dotnet build Wms.sln` **0/0**; **160 test hijau** — Outbound.Domain **25** (OutboundOrder/Wave/PickingTask + event-emission behavioral), Outbound.Integration **8** (migration uuid[]/owned round-trip + 6 slice/consumer + **full E2E**), Inventory.Domain 18, Inbound.Domain 48, BuildingBlocks 30, Inventory.Integration 10, Inbound.Integration 13, Architecture FF **8/8**. Migration `InitialOutbound` apply bersih di Postgres riil (Testcontainers).

**Dibangun (instansiasi template — NOL building-block baru):**
- **Modul Outbound penuh** (5 project baru: Domain/Application/Infrastructure/Api + Host.Local; Contracts sudah dari 03b). Domain TDD RED→GREEN: `OutboundOrder` (New·InProgress·Closed + owned `OrderLine`), `Wave` (Active·Ready·Dispatched + `OrderIds`/`PickingTaskIds` primitive-collection), `PickingTask` (Assigned·Completed). Strongly-typed id, Result no-throw (FF#7), errors taxonomy.
- **Gaya emit (keputusan sadar, ADR-0026):** single-aggregate fact → domain event di-raise aggregate lalu di-translate handler (`PickingCompleted`→`PickingCompletedV1`; `ShipmentDispatched`→`ShipmentDispatchedV1`). Cross-aggregate aggregation → handler compose langsung (`WaveReleasedV1` mengagregasi orderLines lintas-order — pola sama `StockAllocated` di Inventory; Wave tak membawa data milik OutboundOrder).
- **4 slice REST** (`ReceiveOutboundOrder`→New, `CreateWave`→emit WaveReleased, `CompletePicking`→emit PickingCompleted + gate Wave→Ready, `DispatchWave`→emit ShipmentDispatched + close orders) + **1 consumer** `StockAllocated` (Inbox idempotent → PickingTask per allocation + `Wave.AttachPickingTasks`). `// TODO-AUTH` di CreateWave/CompletePicking/DispatchWave (ReceiveOrder tak ada di permission catalog §E).
- **Gate Wave→Ready = aturan agregasi di domain** (`Wave.MarkReady(completedIds)` verifikasi semua pickingTaskId ∈ completed; handler suplai fakta dari query; `NotAllPicked` = belum siap, di-swallow non-fatal).
- **Infra:** `OutboundDbContext` (schema `outbound`), owned `order_lines`, **EF Core 8 primitive collection** `order_ids`/`picking_task_ids` → `uuid[]` (Npgsql) via backing-field + Ignore read-only accessor. Dispatcher route `StockAllocatedV1`. Host hybrid (producer 3 event + consumer + REST). AppHost +`outbounddb`+host; MigrationRunner +Outbound.
- **FF `ModuleLayers["Outbound"]` → full-5** (+ ref di `Architecture.Tests`). asyncapi **tak berubah** (5 channel sudah diratifikasi 03b; FF#11 hijau).

**E2E (CoreFlowE2ETests, Opsi C 1-proses):** 2 order → wave → relay WaveReleased → 2 Stock Allocated → 2 PickingTask → CompletePicking ×2 → Wave Ready → relay PickingCompleted → 2 Stock Picked → DispatchWave → orders Closed + relay ShipmentDispatched → Stock removed. Loop tertutup, terverifikasi Postgres riil.

**Utang sadar / gap:** (1) Aspire **live** dashboard smoke (`dotnet run AppHost`) = manual, belum dijalankan — cross-process delivery IDLE di Local (broker Phase 05/06); E2E otoritatif via integration test 1-proses (by design, host comment). (2) Allocation failure / picking discrepancy / wave cancel = out-of-scope global (tak dibangun). (3) AuthZ deferred (`// TODO-AUTH`)→07a. (4) `OutboundSeed.DefaultUom`="carton" LOCAL SEED sampai MasterData 04a.

**Phase 04** menambah supporting services: **04a MasterData menggantikan seed snapshot** (read-API gRPC + cache-aside), 04b Auth, 04c Reporting, 04d Notification, 04e WebUI+Gateway.

**Touchpoint cert:** AZ-204 — Service Bus messaging *(pattern)* + Azure DB for PostgreSQL *(EF)* → X. PCD — Pub/Sub *(pattern)* + Cloud SQL → X.

**Out-of-scope:** ⚠ wave reschedule/cancel (kalau Wave dibatalkan, release Allocated→Available), picking discrepancy (`actualQty<qty`) — flag sebagai gap, JANGAN dibangun (out-of-scope global; saga cancel tetap deferred per ADR-0005).
