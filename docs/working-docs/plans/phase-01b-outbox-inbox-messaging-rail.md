# Phase 01b — Outbox + Inbox Messaging Rail (Local)

**Status:** planned

**Pre-conditions:**
- **01a done:** `Wms.sln` build hijau, 6 FF hijau, BuildingBlocks seedwork ada, AppHost Aspire boot Postgres.
- Bagian dari Phase 01 Walking Skeleton (deliverable E2E di 01c).

**Context refs (WAJIB):**
- `docs/adr/0005-event-driven-outbox.md` (Outbox, domain→integration event, **composite inbox key `(event_id, handler_type)`**, logical event id)
- `docs/adr/0010-data-ownership-db-per-service.md` (infra-table ownership, shared `modelBuilder` ext, `MigrationRunner`, `IDeadLetterStore`)
- `docs/adr/0024-cross-broker-trace-context-propagation.md` (envelope field `traceparent`/`tracestate`)
- `docs/architecture/tri-cloud-microservices-blueprint.md` (§2 aliran antar-context)

**Tujuan:** Bangun rail messaging reusable — Outbox dispatcher + Inbox idempotency + `IMessagePublisher` (Local adapter) + message envelope — building block yang dipakai SEMUA event antar-service.

**Deliverable:**
- `Wms.BuildingBlocks.Application`: port `IMessagePublisher`.
- `Wms.BuildingBlocks.Infrastructure`: `MessageEnvelope` (eventId, occurredAt, logicalName, `traceparent`, `tracestate`, payload), entity `OutboxMessage`/`InboxMessage`, shared ext `AddInfrastructureTables(modelBuilder)` (schema `infrastructure`: `outbox`, `inbox` PK komposit `(event_id, handler_type)`, `dead_letter`), `OutboxDispatcher : BackgroundService`, Inbox dedup helper, `IDeadLetterStore`.
- `Wms.Platform.Local`: `InMemoryMessagePublisher` (in-proc channel) + `LocalDeadLetterStore` (Postgres `dead_letter`).
- `src/Tools/Wms.MigrationRunner` (console — apply EF migration per service DB, NOL cloud SDK).
- `tests/Wms.Inbound.IntegrationTests` (Testcontainers Postgres) — harness reusable.

**Tasks:**
1. `IMessagePublisher.PublishAsync(MessageEnvelope)` di `BuildingBlocks.Application`.
2. `MessageEnvelope` + `OutboxMessage`/`InboxMessage` + `AddInfrastructureTables()` map ke schema `infrastructure` (outbox/inbox/dead_letter); inbox PK komposit `(event_id, handler_type)`.
3. `OutboxDispatcher` — poll outbox unsent → publish via `IMessagePublisher` → mark dispatched; retry, max → `IDeadLetterStore`.
4. Inbox dedup helper: `HasProcessed(eventId, handlerType)` + `MarkProcessed(...)` commit **satu tx** dgn business write.
5. `InMemoryMessagePublisher` + `LocalDeadLetterStore` di `Platform.Local`.
6. `Wms.MigrationRunner` console (connection string via config).
7. `Wms.Inbound.IntegrationTests` (Testcontainers Postgres): test round-trip (publish→dispatch→consume) + idempotency (duplicate event → handler jalan sekali).

**Definition of Done:**
- `dotnet build` hijau; **6 FF tetap hijau**.
- `dotnet test tests/Wms.Inbound.IntegrationTests` hijau: round-trip + idempotency (duplicate suppressed) pass.
- `Wms.MigrationRunner` apply migration outbox/inbox/dead_letter ke Postgres lokal sukses.

**Learning objective:** Transactional Outbox (anti dual-write), Inbox idempotency dgn composite key (multi-consumer safe), Dead Letter Channel (EIP), message envelope + trace-context seam, EF migration ops.

**Handoff notes:** Rail messaging siap-pakai — produser tulis integration event ke Outbox; konsumer pakai Inbox dedup; envelope sudah carry `traceparent` (propagasi penuh di 07b). **01c** memakai rail ini untuk event nyata pertama: `GRConfirmed` Inbound→Inventory.

**Touchpoint cert:** AZ-204 — Service Bus / message-based solution *(pattern; broker konkret di 05)*. PCD — Pub/Sub / async idempotency *(pattern; broker konkret di 06)*.
