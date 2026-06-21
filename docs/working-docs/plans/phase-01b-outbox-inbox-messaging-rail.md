# Phase 01b — Outbox + Inbox Messaging Rail (Local)

**Status:** done (2026-06-21)

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

### State akhir repo (2026-06-21)
- **DoD hijau semua:** `dotnet build Wms.sln` 0/0 · 6 FF hijau · `tests/Wms.Inbound.IntegrationTests` 4/4 (round-trip · duplicate-suppressed · poison→DLQ · migration-applies) · `Wms.MigrationRunner` apply `infrastructure.{outbox,inbox,dead_letter}` ke Postgres lokal (idempotent saat re-run; verified `pk_inbox=(event_id,handler_type)`).
- **Stack baru (CPM):** EF Core 8.0.15 (+Relational/Design) · Npgsql.EFCore 8.0.11 · EFCore.NamingConventions 8.0.3 (snake_case) · Microsoft.Extensions.Hosting(.Abstractions) 8.0.1 · Testcontainers.PostgreSql 4.12.0.
- **Keputusan sadar (deviasi terflag dari teks literal phase):**
  1. `MessageEnvelope` + port `IMessagePublisher`/`IDeadLetterStore` + DTO `DeadLetterMessage` ditempatkan di **`BuildingBlocks.Application`** (bukan `.Infrastructure` seperti bullet deliverable). Alasan: port "berbicara" envelope; menaruh envelope di Infrastructure memaksa Application→Infrastructure (langgar Clean Arch dependency rule + bikin `Platform.Local` butuh Infrastructure). Entity mekanisme `OutboxMessage`/`InboxMessage` tetap di Infrastructure. FF#5 tidak menjaring kasus BuildingBlocks ini, tapi aturan tetap ditaati manual.
  2. **snake_case** kolom/tabel via `EFCore.NamingConventions` di seam `UseNpgsql` — konvensi DB Postgres ditetapkan di persistence pertama project (idiom; matches overview `gr_scanned_lines`).
  3. **`VersionOverride` 10.0.8** untuk `Microsoft.Extensions.Hosting*` di `Wms.AppHost` saja — memutus konflik CPM transitive-pin (services net8 = 8.0.1 vs AppHost net10/Aspire ≥10.0.8). Pola berulang untuk paket `Microsoft.Extensions.*` mendatang.
- **Trace-context:** envelope membawa `traceparent`/`tracestate` (kolom + map outbox), tapi **belum di-restart Activity** di consumer — itu 07b. Saat ini null-pass aman.
- **Catatan utang teknis (di luar scope DoD, untuk fase lanjut):**
  - `DateTimeOffset.UtcNow` dipakai langsung di dispatcher/producer — seam `TimeProvider`/clock di 02c (audit baseline).
  - Dead-letter pada poison: `StoreAsync` + mark-processed = 2 SaveChanges → window duplikat-on-crash (at-least-once forensik, dapat dedup by `event_id`). Acceptable; revisit bila perlu atomic.
  - Inbox concurrent-duplicate mengandalkan composite-PK unique violation; handler produksi (01c) sebaiknya catch `DbUpdateException` → treat as processed.

**Touchpoint cert:** AZ-204 — Service Bus / message-based solution *(pattern; broker konkret di 05)*. PCD — Pub/Sub / async idempotency *(pattern; broker konkret di 06)*.
