# Phase 01c — Inbound→Inventory Event Chain (Walking Skeleton E2E)

**Status:** done (2026-06-21)

**Pre-conditions:**
- **01b done:** rail Outbox/Inbox + `IMessagePublisher` Local + envelope + IntegrationTests harness ada; `MigrationRunner` jalan.
- Penutup **Phase 01** — akhir sub-phase ini = **walking skeleton E2E selesai** (deliverable pertama project).

**Context refs (WAJIB):**
- `docs/tomsandboxwms-overview.md` (§A Inbound flow A1–A4, §B Inventory flow B1–B2, event `GRConfirmed`)
- `docs/adr/0005-event-driven-outbox.md` · `docs/adr/0009-contracts-vs-grpc-separation.md`
- `docs/adr/0004-cqrs-vertical-slice.md` (Features/<UseCase>) · `docs/adr/0026-tactical-ddd-conventions.md` (emission policy)

**Tujuan:** Wire thread E2E tertipis yang meng-exercise core event chain: Inbound buat & confirm `GoodsReceipt` → emit `GRConfirmed` lewat Outbox → Inventory consume (Inbox dedup) → create `Stock`(OnHand) + `PutawayTask`(Assigned).

**Deliverable:**
- `Wms.Inbound.Domain`: `GoodsReceipt` **minimal** (Create header dgn `expectedLines` seed; `Confirm()` raise domain event). **Belum** ada discrepancy logic (itu 03a); product/uom via seed lokal (MasterData belum ada).
- `Wms.Inbound.Contracts`: `GRConfirmedV1` (logical `inbound.gr_confirmed.v1`; payload `grId, warehouseId, receivedLines[]`).
- `Wms.Inbound.Application`: `Features/CreateGoodsReceipt`, `Features/ConfirmGoodsReceipt` (translate domain event → `GRConfirmedV1` → Outbox).
- `Wms.Inbound.Api`: REST endpoint create + confirm; marker `// TODO-AUTH: Inbound.CreateGR` / `// TODO-AUTH: Inbound.PostGR`.
- `Wms.Inventory.Domain`: `Stock` (state OnHand) + `PutawayTask` (state Assigned) minimal.
- `Wms.Inventory.Application`: handler consume `GRConfirmedV1` (Inbox dedup) → per receivedLine create Stock(OnHand)+PutawayTask.
- Kedua host ter-declare di `Wms.AppHost`.

**Tasks:**
1. `GoodsReceipt` aggregate minimal (factory `Create`, `Confirm()` raise `GoodsReceiptConfirmed` domain event — emission dari dalam aggregate, ADR-0026).
2. `GRConfirmedV1` di Contracts + atribut logical name `inbound.gr_confirmed.v1`.
3. Slice CreateGoodsReceipt + ConfirmGoodsReceipt; saat confirm, translate domain→integration event → tulis Outbox (satu tx dgn state).
4. REST endpoint di `Inbound.Api` + register host; pasang marker `// TODO-AUTH`.
5. Inventory `Stock`/`PutawayTask` minimal aggregate.
6. Inventory handler consume `GRConfirmedV1` via Inbox dedup → create Stock(OnHand)+PutawayTask per line; commit projection-write + inbox mark **satu tx**.
7. Declare Inventory host + dependency di AppHost.
8. E2E integration test + idempotency test.

**Definition of Done:**
- `dotnet build` hijau; **6 FF hijau**.
- `dotnet test` hijau: **E2E** (confirm GR → Stock(OnHand)+PutawayTask tercipta) + **idempotency** (duplicate `GRConfirmed` → Stock dibuat sekali).
- Smoke: `dotnet run --project src/AppHost/Wms.AppHost` → POST create+confirm GR via REST → state Inventory terbentuk. **Walking skeleton jalan.**

**Learning objective:** Walking skeleton (Cockburn), vertical slice CQRS, domain event → integration event translation (published language), choreography EDA E2E, idempotent consumer.

**Handoff notes:** **WALKING SKELETON COMPLETE** — core event chain Inbound→Inventory hidup lokal via Aspire (Outbox+Inbox). Domain masih thin (no discrepancy; Stock cuma OnHand→tidak ada putaway-complete). **Phase 02 harden building blocks SEBELUM ekspansi** (prinsip 2). Outbound + domain penuh menyusul di Phase 03. **Out-of-scope:** QC release, return-to-vendor.

---

### Handoff aktual — 2026-06-21

**Keputusan kunci:** Local messaging delivery → **ADR-0029** (in-proc; walking-skeleton E2E dibuktikan via integration test 1-proses; cross-process delivery di-defer ke broker cloud Phase 05/06). Dua host tetap di AppHost.

**Yang dibangun:**
- **Inbound** (producer): `GoodsReceipt` aggregate (Create/Confirm→`GoodsReceiptConfirmed`), `GRConfirmedV1` (logical `inbound.gr_confirmed.v1`, `const LogicalName`), slice `CreateGoodsReceipt`/`ConfirmGoodsReceipt` (domain→integration event→Outbox, 1 tx), REST `POST /goods-receipts` + `/{id}/confirm` (+ `TODO-AUTH` markers), EF mapping + migration `AddGoodsReceipt`.
- **Inventory** (consumer, modul baru): `Stock`(OnHand) + `PutawayTask`(Assigned), consumer `GRConfirmedV1` (Inbox dedup → Stock+PutawayTask, 1 tx), `InventoryDbContext` + migration `InitialInventory`, `InventoryIntegrationEventDispatcher` (consumer subscribe-point; idle di Local 2-proses, disambung broker P05/06), `Host.Local` + AppHost + MigrationRunner.
- **BuildingBlocks (baru):** port `IUnitOfWork`, `IIntegrationEventOutbox`, `IInboxGuard` + adapter EF + `AddTransactionalMessaging()`.
- **FF:** `ArchitectureFitnessFunctions` jadi data-driven per-modul; **FF#3 aktif** (Inventory hanya ref `Inbound.Contracts`). 6/6 hijau.
- **Tests:** `Wms.TestSupport` (shared `PostgresFixture`), domain unit tests (Inbound 9 + Inventory 4), **E2E + idempotency** (`Wms.Inventory.IntegrationTests` 2/2). Total 25 test hijau; `dotnet build` 0 warning (TreatWarningsAsErrors).

**Belum dijalankan (rekomendasi manual Tom):** live Aspire `dotnet run --project src/AppHost/Wms.AppHost` (F5 dev-experience) + `dotnet run --project src/Tools/Wms.MigrationRunner` untuk apply skema ke DB Aspire. Per ADR-0029 ini bukan gate (walking skeleton dibuktikan via E2E test); cross-process state Inventory via AppHost baru terjadi saat broker cloud.

**Deviasi sadar (vs DoD/roadmap):** (1) logical name = `const string` bukan attribute (jaga Contracts dependency-free; attribute/registry di P02b). (2) DoD *smoke* di-relax per ADR-0029. (3) +domain unit tests & +`Wms.TestSupport` (di atas DoD minimum, selaras ADR-0026 + DRY).

**Touchpoint cert:** AZ-204 — Service Bus messaging *(pattern)* + Azure DB for PostgreSQL *(EF persistence pattern)*. PCD — Pub/Sub *(pattern)* + Cloud SQL *(pattern)*.
