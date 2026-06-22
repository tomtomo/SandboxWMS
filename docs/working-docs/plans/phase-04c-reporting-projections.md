# Phase 04c — Reporting: Inbox-Committed Projections (CQRS Read-Side)

**Status:** done (2026-06-22)

**Pre-conditions:**
- **03c done:** core flow E2E hijau; events `inbound.gr_confirmed.v1`, `outbound.shipment_dispatched.v1`, `outbound.picking_completed.v1` + PutawayTask-Completed di-emit lewat rel Outbox.
- Rel Outbox/Inbox + `IDeadLetterStore` + IntegrationTests harness (Phase 01b/02b) terpasang. Reporting & 04a/04b tiga track independen di bawah Phase 04.

**Context refs (WAJIB):**
- `docs/adr/0017-eventual-consistency-reporting-notification.md` (pure consumer + projection rebuild-able + Inbox-committed atomicity + per-type store, generic/Cosmos REJECTED)
- `docs/tomsandboxwms-overview.md` §F

**Tujuan:** Berdirikan Reporting (pure consumer, collapsed 1 project) — bangun projection denormalized dari domain event core via eventual consistency; query REST untuk dashboard; rebuild-able dari event.

**Deliverable:**
- `Wms.Reporting` module (pure consumer, collapsed **1 project** + Contracts kalau perlu konsumsi). Projection `StockOnHandView` / `ReceivingSummary` / `DispatchSummary` / `OperatorActivity` (tabel Postgres denormalized, schema `reporting`).
- **Per-type** port `I*Store` (`IStockOnHandViewStore` dst) + adapter EF — generic `IProjectionStore<T>` + adapter Cosmos **REJECTED** (NoSQL deferred ke spike).
- Event handlers konsumsi `inbound.gr_confirmed.v1` → `ReceivingSummary` + `StockOnHandView`; `outbound.shipment_dispatched.v1` → `DispatchSummary` + `StockOnHandView`; PutawayTask-Completed → `OperatorActivity`; `outbound.picking_completed.v1` → `OperatorActivity`.
- **Inbox-committed atomicity**: handler find-or-create-by-PK + mutate via store, **TIDAK** panggil `SaveChanges` sendiri — consumer sisi Inbox commit projection-write **+** mark idempotency Inbox dalam **satu transaksi**.
- Query REST API untuk dashboard (`Wms.Reporting` endpoints).
- Rebuild-from-events capability (replay event → rekonstruksi projection).

**Tasks:**
1. `Wms.Reporting` (1 project) DbContext schema `reporting` + tabel denormalized 4 projection.
2. Per-type `I*Store` ports + EF adapters (jangan bikin generic/Cosmos seam).
3. Event handler `inbound.gr_confirmed.v1` → `ReceivingSummary` (discrepancy rate per supplier) + `StockOnHandView` per `receivedLine`.
4. Event handler `outbound.shipment_dispatched.v1` → `DispatchSummary` + kurang `StockOnHandView`; PutawayTask-Completed + `outbound.picking_completed.v1` → `OperatorActivity`.
5. Pastikan handler **tak** panggil `SaveChanges`; commit projection-write + Inbox-mark satu tx di consumer sisi Inbox.
6. Query REST endpoints (stock-on-hand, receiving/dispatch summary, operator productivity).
7. Rebuild-from-events path (replay → rekonstruksi).
8. `Wms.Reporting.Host.Local` declare di `Wms.AppHost`. Integration: exactly-once, query, rebuild tests.

**Definition of Done:**
- `dotnet build Wms.sln` hijau; **semua FF hijau**.
- Integration: event meng-update projection **exactly-once** (duplicate → **no double-count**); query endpoint mengembalikan projection; **rebuild test** (replay events → projection terkonstruksi ulang identik).

**Out-of-scope:** Migrasi projection ke NoSQL/Cosmos/Firestore (deferred ke spike; generic store ditolak). Event store dedicated jangka-panjang (pakai Outbox retention). Real-time strict reporting. Konsumsi `StockLow`/`StockNearExpiry` (emitted-but-unconsumed gap, biarkan).

**Learning objective:** CQRS read-side + eventual consistency, projection rebuild-able (derived data), **Inbox-committed projection atomicity** (cegah double-count/lost di partial failure), per-type store (hindari premature generic/NoSQL abstraction).

**Handoff notes (state akhir 2026-06-22):** **REPORTING HIDUP sebagai pure consumer.** `dotnet build Wms.sln` **0/0**; full suite **hijau** — Reporting.Integration **6** (GRConfirmed→ReceivingSummary+StockOnHand · exactly-once duplicate→no-double-count · StockRemoved decrement+DispatchSummary · Putaway+Picking→OperatorActivity · query endpoint REST via WebApplicationFactory · rebuild-replay→identik), Architecture FF **15/15** (FF#1+FF#3 kini meng-inspect collapsed module). Migration `InitialReporting` apply bersih di Postgres riil (Testcontainers, via MigrateAsync tiap test).

**Keputusan kunci — [ADR-0030](../../adr/0030-reporting-event-enrichment.md) (event-carried state transfer):** Pre-condition gap ditemukan saat sequencing — event 03a–03c adalah notifikasi tipis; Reporting (DB-per-service) tak bisa derive. Resolusi (Option A, dipilih Tom): enrich producer di-emit pemilik datanya.
- **Enrich non-breaking (tetap v1):** `inbound.gr_confirmed.v1` +`SupplierId` (data domain, lewat domain event `GoodsReceiptConfirmed`) · `outbound.picking_completed.v1` +`OperatorId` (aktor, di-source `ICurrentUser` di handler, BUKAN domain event).
- **2 event baru owner-emitted (Inventory):** `inventory.putaway_completed.v1` (emit di `CompletePutaway`) · `inventory.stock_removed.v1` (emit saat Inventory hapus Stock Picked di `ShipmentDispatched`; bawa lines warehouse/sku/batch/qty). Katalog asyncapi 5→7; FF#11 menjaga keduanya.
- **Divergence §F-literal (sadar):** StockOnHandView-decrement + DispatchSummary di-feed `inventory.stock_removed.v1` (ownership-correct), BUKAN `outbound.shipment_dispatched.v1` apa adanya (yang cuma `waveId`). Intent §F dipenuhi; sumber event ownership-correct.

**Dibangun:**
- **Modul `Wms.Reporting` (collapsed 1 project, blueprint §3 right-sizing):** 4 projection denormalized (`StockOnHandView`/`ReceivingSummary`/`DispatchSummary`/`OperatorActivity`, schema `reporting`, composite natural PK) · per-type `I*Store` + adapter EF (find-or-create-by-PK, **NO SaveChanges** — generic/Cosmos DITOLAK per ADR-0017) · 4 projector Inbox-committed (store-mutate → MarkProcessed → UoW.SaveChanges **satu transaksi**) · `ReportingIntegrationEventDispatcher` (route + teruskan `OccurredAt` envelope → bucket hari deterministik untuk rebuild) · 4 query REST read-side (`/reports/*`, baca DbContext langsung, TODO-AUTH) · `ProjectionRebuilder.ResetAsync` (reset projeksi+Inbox → replay).
- **Host `Wms.Reporting.Host.Local`** (consumer subscribe-point + query API; nol Outbox/auth) di-declare di `Wms.AppHost` (+`reportingdb`). MigrationRunner +Reporting. FF harness +`CollapsedModules`. sln + `Wms.Local.slnf` +3 project.
- **Producer enrichment:** Inbound (`GoodsReceiptConfirmed`+supplier), Outbound (`CompletePickingHandler` inject `ICurrentUser`), Inventory (`CompletePutawayHandler` + `ShipmentDispatchedConsumer` emit 2 event baru, `Wms.Inventory.Contracts` +`PutawayCompletedV1`/`StockRemovedV1`).

**Utang sadar / gap:** (1) **`operatorId` = SYSTEM** sampai authZ wire-up (07a, ADR-0012) — mekanisme OperatorActivity lengkap, atribusi per-operator nyata menyala nanti. (2) **scan-count** (overview §F OperatorActivity) tak di-scope — tak ada integration event untuk ScanItem (Inbound internal); §F mapping hanya Putaway/Picking→OperatorActivity. (3) Cross-process rail delivery IDLE di Local (broker P05d/06d); projector E2E diverifikasi via integration test 1-proses invoke-langsung (by design, ADR-0029) — host subscribe + DLQ wrap = building block sama Inventory (02b). (4) Aspire **live** smoke (`dotnet run AppHost`) manual, belum dijalankan. (5) NoSQL/Cosmos projection store deferred ke spike (ADR-0017).

Reporting → dipakai WebUI di 04e (stock view + reports). Profil serverless (event-triggered) sets up Azure Functions (05d) / Cloud Functions gen2 (06d).

**Touchpoint cert:** AZ-204 — Azure Functions trigger + Service Bus (pattern; serverless branded 05d) + Cosmos DB (deferred-spike note). PCD — Cloud Functions gen2 + Pub/Sub (pattern; serverless branded 06d).
