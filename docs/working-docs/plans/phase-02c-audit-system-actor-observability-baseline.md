# Phase 02c — Audit Log + SYSTEM Actor + Observability Baseline

**Status:** planned

**Pre-conditions:**
- **02b done:** `asyncapi.yaml` + FF #11 + emission behavioral test + consumer retry/DLQ baseline hijau; pipeline behavior baseline ada (slot `AuditLogBehavior` masih placeholder dari 02a).
- Penutup **Phase 02 Harden** (prinsip 2): melengkapi BASELINE template. Observability/audit DEEP (W3C cross-broker trace penuh, App Insights/Cloud Trace, retensi audit) tetap **ditunda Phase 07** (prinsip 5).

**Context refs (WAJIB baca dulu):**
- `docs/adr/0027-system-actor-convention.md` (`ICurrentUser`→SYSTEM saat `HttpContext` null; invariant: anonymous HTTP `IsAuthenticated=false` **tak boleh** ambil cabang SYSTEM; pure Application-layer)
- `docs/adr/0022-operational-audit-log.md` (`AuditLogBehavior` MediatR, `*Command` mutasi only; outcome-aware `IsSuccess`+`ErrorCode`; out-of-band own connection/tx → survive rollback; **bukan Outbox**; `IAuditableCommand` eksplisit; PII-redaction predicate)
- `docs/adr/0024-cross-broker-trace-context-propagation.md` (**baseline** — correlation kuat dengan correlation-id `BuildingBlocks.Web`; cross-broker W3C penuh = Phase 07)
- `docs/adr/0008-aspire-distributed-local.md` (`AddServiceDefaults` di `Platform.Hosting`: logging/telemetry/health, nol cloud SDK)
- `docs/adr/0012-deferred-authorization-enforcement.md` (authN→identity ke `IAuditable`; authZ enforce tetap deferred)

**Tujuan:** Tutup building-block template — pasang SYSTEM actor convention, audit log out-of-band yang survive rollback, kolom `IAuditable` auto-populate, dan OTel baseline + correlation-id sehingga Inbound/Inventory menampilkan template lengkap.

**Deliverable:**
- `Wms.BuildingBlocks.Application`: `ICurrentUser` → resolve **SYSTEM** saat `HttpContext` null (di-key pada HttpContext-is-null, **bukan** `!IsAuthenticated`; invariant anon≠SYSTEM ditegakkan unit test). Interface `IAuditableCommand` (supply `AggregateType`/`AggregateId`).
- `IAuditable` (createdBy/createdAt/modifiedBy/modifiedAt) + EF `SaveChanges` interceptor populate dari `ICurrentUser`.
- `IAuditLogStore` port + `AuditLogBehavior` (MediatR; **mutating `*Command` only**; outcome-aware `IsSuccess`+`ErrorCode` dari `Result`; **out-of-band own connection/tx** → survive rollback; eksplisit **bukan Outbox**) + **PII-redaction predicate**.
- Tabel `audit_log` ditambah ke schema `infrastructure` via extension `AddInfrastructureTables` (dimiliki DbContext modul, ADR-0010 — bukan `InfrastructureDbContext` standalone).
- `LocalAuditLogStore` adapter (`Wms.Platform.Local`).
- `Wms.Platform.Hosting` `AddServiceDefaults`: **OTel baseline** (traces/metrics/logs) + correlation-id middleware di `Wms.BuildingBlocks.Web`.

**Tasks:**
1. `ICurrentUser` implementasi: cabang SYSTEM saat `HttpContext` null; unit test invariant anon≠SYSTEM (HttpContext ada + `IsAuthenticated=false` → **tidak** SYSTEM).
2. `IAuditable` interface + EF `SaveChanges` interceptor isi createdBy/createdAt/modifiedBy/modifiedAt dari `ICurrentUser`.
3. `IAuditLogStore` port + `LocalAuditLogStore` adapter di `Wms.Platform.Local`.
4. `AuditLogBehavior` (MediatR): hanya `*Command` mutasi yang `IAuditableCommand`; rekam `IsSuccess`+`ErrorCode` dari `Result`; tulis lewat `IAuditLogStore` di **koneksi/tx sendiri** (survive rollback bisnis); terapkan PII-redaction predicate. **Tidak** lewat Outbox.
5. Sisipkan `AuditLogBehavior` ke pipeline (isi slot placeholder 02a; tetap hormati order — audit membungkus Transaction agar attempt gagal terekam).
6. Tambah tabel `audit_log` ke schema `infrastructure` via `AddInfrastructureTables` extension (apply lewat `MigrationRunner`).
7. Tandai command mutasi 01c (`ConfirmGoodsReceipt`) sebagai `IAuditableCommand` (supply `AggregateType`/`AggregateId`).
8. OTel baseline di `AddServiceDefaults` (`Wms.Platform.Hosting`): traces/metrics/logs; tambah correlation-id middleware di `Wms.BuildingBlocks.Web`.
9. Unit test SYSTEM-actor invariant + behavioral test "audited command writes audit entry even when business tx rolls back".

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **semua FF (#1–#11 + #7) pass** (tak ada regresi).
- Unit test SYSTEM-actor invariant hijau (anonymous HTTP tak ambil cabang SYSTEM).
- Behavioral test hijau: audited command yang `Result.Failure` → bisnis rollback **tapi** baris `audit_log` tetap tertulis (out-of-band).
- Smoke: `dotnet run --project src/AppHost/Wms.AppHost` → Aspire dashboard menampilkan traces + correlation-id ter-propagasi lintas Inbound→Inventory.

**Learning objective:** SYSTEM actor convention (HttpContext-is-null, bukan `!IsAuthenticated`); operational audit log out-of-band (survive rollback, bukan Outbox); `IAuditable` auto-populate; OpenTelemetry baseline + correlation-id; distributed tracing intro.

**Out-of-scope:** cross-broker W3C trace-context propagation penuh (ADR-0024 → Phase 07b); audit read/query UI + retensi/arsip (ADR-0022 → deferred, handler dorman); warehouse-scoping & authZ enforce (ADR-0012/0027 → Phase 07a).

**Handoff notes:** **BUILDING-BLOCK TEMPLATE COMPLETE** — Inbound/Inventory kini memamerkan template penuh: `Result`, validation, transaction, audit out-of-band, SYSTEM actor, OTel baseline, emission FF, contract catalog. **Phase 03+ ekspansi hanya meng-INSTANSIASI template ini — JANGAN reinvent pattern** (Result/pipeline/audit/emission/contract sudah baku). Cross-cutting DEEP menyusul di Phase 07.

**Touchpoint cert:** AZ-204 — Application Insights / OpenTelemetry *(baseline, pattern)* → X. PCD — Cloud Trace / Cloud Monitoring *(baseline, pattern)* → X.
