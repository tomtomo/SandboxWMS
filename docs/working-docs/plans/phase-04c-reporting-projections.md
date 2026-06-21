# Phase 04c — Reporting: Inbox-Committed Projections (CQRS Read-Side)

**Status:** planned

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

**Handoff notes:** Reporting hidup sebagai pure consumer; projection + query REST + rebuild siap → dipakai WebUI di 04e (stock view + reports). Pola serverless-profile (event-triggered) sets up Azure Functions (05d) / Cloud Functions gen2 (06d).

**Touchpoint cert:** AZ-204 — Azure Functions trigger + Service Bus (pattern; serverless branded 05d) + Cosmos DB (deferred-spike note). PCD — Cloud Functions gen2 + Pub/Sub (pattern; serverless branded 06d).
