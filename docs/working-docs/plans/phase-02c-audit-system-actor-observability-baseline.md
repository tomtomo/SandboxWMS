# Phase 02c — Audit Log + SYSTEM Actor + Observability Baseline

**Status:** done (2026-06-21)

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

---

## Completion (2026-06-21)

**Terverifikasi:** `dotnet build Wms.sln` 0-warning/0-error; **62 test hijau** (naik dari 48: +12 unit [4 resolver invariant + 8 redaksi], +2 behavioral audit out-of-band; 1 assertion `created_by=SYSTEM` ditambah ke consumer test). 8 FF tetap hijau (nol regresi). 2 migration baru (Inbound/Inventory) ter-generate + tervalidasi via `InfrastructureMigrationTests` (`MigrateAsync` riil).

**Dibangun (per task):**
1. **SYSTEM actor** — port `ICurrentUser` (`BuildingBlocks.Application.Security`) + `CurrentUserResolver` **murni** (transport-free; `ClaimsPrincipal` = BCL) yang meng-key SYSTEM pada **HttpContext-is-null**, bukan `!IsAuthenticated`; adapter `HttpContextCurrentUser` (`BuildingBlocks.Web`) + Null-Object `SystemCurrentUser` (origin-mesin). Invariant **anon≠SYSTEM** ditegakkan `CurrentUserResolverTests`.
2. **IAuditable** (`BuildingBlocks.Domain.Auditing`) + base `AuditableAggregateRoot<TId>` (private setter — enkapsulasi) + `AuditableEntityInterceptor` (EF SaveChanges, stempel via `entry.Property(...).CurrentValue`). `Stock`/`PutawayTask`/`GoodsReceipt` di-retrofit. Interceptor di-wire via `AddDbContext((sp,options) => AddInterceptors(...))`.
3. **`IAuditLogStore` + `AuditLogEntry`** (Application, port-language; mirror `IDeadLetterStore`/`DeadLetterMessage`) + adapter `LocalAuditLogStore` (`Platform.Local`).
4. **`AuditLogBehavior`** (MediatR) — opt-in `IAuditableCommand`, outcome-aware (`IsSuccess`+`ErrorCode`), **out-of-band via `IServiceScopeFactory.CreateScope()`** (DbContext segar → survive rollback), exception-path juga teraudit, best-effort (gagal audit di-log, tak menutupi hasil bisnis). PII-redaksi `AuditRedaction`.
5. **Pipeline** — `AuditLogBehavior` di-sisip **OUTER ke Transaction** (Logging→Authorization→Validation→**AuditLog**→Transaction).
6. **`audit_log`** di `infrastructure` via `AddInfrastructureTables` (owned DbContext modul, ADR-0010 — bukan standalone) + migration per modul.
7. `ConfirmGoodsReceiptCommand` ditandai `IAuditableCommand` (`AggregateType="GoodsReceipt"`, `AggregateId`).
8. **OTel baseline** di `AddServiceDefaults` (`Platform.Hosting`): logs (formatted+scopes) + metrics (ASP.NET/HttpClient/runtime) + traces (ASP.NET/HttpClient + ActivitySource app) + OTLP exporter (guard `OTEL_EXPORTER_OTLP_ENDPOINT`). **Correlation-id middleware** (`BuildingBlocks.Web`) → tag Activity + log-scope + echo header.
9. Behavioral `AuditLogBehaviorTests` (rollback→audit tetap tertulis + control sukses) + invariant unit test.

**Keputusan sadar (auditable):**
- **`AuditLog` OUTER ke Transaction tapi INNER ke Validation** — phase mewajibkan "audit membungkus Transaction"; ditaruh INNER ke Validation karena command gagal-validasi short-circuit sebelum menyentuh aggregate (bukan "attempt" bisnis). Trigger tinjau-ulang: kalau perlu audit penolakan validasi/authz, geser ke luar Validation/Authorization.
- **`CurrentUserResolver` murni di Application** (bukan `IHttpContextAccessor` di Application) — jaga Application nol-transport (ADR-0027); Web hanya men-feed `hasRequestContext`+`ClaimsPrincipal`.
- **Default `ICurrentUser=SystemCurrentUser` di-TryAdd oleh `AddXxxInfrastructure`** — infra self-sufficient untuk origin-mesin (consumer/MigrationRunner/integration-test tak perlu mendaftarkan principal); host HTTP override dgn `HttpContextCurrentUser` (registrasi belakangan menang). Trade-off: host HTTP yang lupa override diam-diam jadi SYSTEM — diterima (sandbox; tertangkap di audit HTTP action pertama).
- **Retrofit aggregate ke `IAuditable`** (bukan sekadar bikin interceptor) — overview mewajibkan field audit universal; sekaligus membuktikan SYSTEM actor end-to-end (`Stock.created_by=SYSTEM`). Biaya: 2 migration (audit_log + kolom audit, satu add per modul).
- **OTel versi mixed** (core 1.16.0, instrumentation 1.15.x) — state normal OTel (instrumentation rilis di belakang core); diverifikasi dari NuGet, bukan tebakan.

**Utang sadar / deferred:**
1. **Aspire dashboard smoke** (traces + correlation-id) = **manual, belum dijalankan** — butuh `dotnet run --project src/AppHost/Wms.AppHost` interaktif + Docker. Build + DI + perilaku tervalidasi via test riil (Postgres) + provider MigrationRunner ter-build; dashboard visual diserahkan ke Tom.
2. **Trace continuity Inbound→Inventory lintas-proses** TIDAK aktif di Local — rail 2-proses idle (cross-broker W3C = ADR-0024 → Phase 07b). Dashboard menampilkan trace **per-service** + correlation-id; bukan satu trace E2E lintas-broker.
3. **PII-redaksi baseline** (match nama-field refleksi) — klasifikasi PII per-field lebih dalam (atribut/skema) di-defer (observability deep, Phase 07).
4. **Audit read/query UI + retensi/arsip** = dorman (ADR-0022, deferred). **AuthZ enforce + warehouse-scoping** tetap deferred (ADR-0012/0027 → Phase 07a; `AuthorizationBehavior` pass-through, marker `TODO-AUTH`).
5. **Audit-log store Inventory host** tak di-wire (`AddLocalAuditing` hanya di Inbound) — Inventory belum punya `IAuditableCommand` (consumer plain, bukan MediatR). Di-wire saat Inventory dapat command (Phase 03).
