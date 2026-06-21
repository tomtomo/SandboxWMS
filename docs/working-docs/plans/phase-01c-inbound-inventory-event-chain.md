# Phase 01c — Inbound→Inventory Event Chain (Walking Skeleton E2E)

**Status:** planned

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

**Touchpoint cert:** AZ-204 — Service Bus messaging *(pattern)* + Azure DB for PostgreSQL *(EF persistence pattern)*. PCD — Pub/Sub *(pattern)* + Cloud SQL *(pattern)*.
