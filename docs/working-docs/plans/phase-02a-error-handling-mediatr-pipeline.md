# Phase 02a — Error Handling + MediatR Pipeline Behaviors

**Status:** planned

**Pre-conditions:**
- **01c done:** walking skeleton E2E hidup (Inbound→Inventory via Outbox/Inbox); handler `CreateGoodsReceipt`/`ConfirmGoodsReceipt` + Inventory consumer ada; 6 FF hijau.
- Pembuka **Phase 02 Harden Building Blocks** (prinsip 2): bikin building block jadi TEMPLATE reusable sebelum ekspansi. Phase 02 = **BASELINE** (error-handling, governance, audit/SYSTEM, OTel baseline); cross-cutting DEEP (authz enforce, observability penuh, resilience calibration, security hardening) **ditunda ke Phase 07** (prinsip 5; ADR "minimum-scaffolding-early, deepened-late").

**Context refs (WAJIB baca dulu):**
- `docs/adr/0019-error-handling-result-transport-mapping.md` (no-throw-for-business, `Error.Type` 5-nilai, satu tabel mapping, rollback-on-`Result.Failure`, FF #7 "minimum viable grep")
- `docs/adr/0004-cqrs-vertical-slice.md` (Amendment: pipeline order `Logging→Authorization→Validation→Transaction→Handler`; `TransactionBehavior` command-side only via `ICommand`)
- `docs/adr/0022-operational-audit-log.md` (slot `AuditLogBehavior` = placeholder note di sini; implementasi penuh 02c)
- `docs/architecture/tri-cloud-microservices-blueprint.md` §4 (dependency rule + FF)

**Tujuan:** Jadikan `Result` pattern *executable end-to-end* — failure bisnis pernah jadi `Result` (bukan exception), dipetakan sekali ke REST (RFC 7807) & gRPC, dan UoW rollback saat `Result.Failure`. Pasang MediatR pipeline behavior baseline berurutan sebagai tulang template.

**Deliverable:**
- `Wms.BuildingBlocks.Web`: extension `ToProblemDetails` (`Result`/`Error` → RFC 7807 `ProblemDetails`) + `ResultExceptionInterceptor` (gRPC `Error`→`RpcException`/`StatusCode`). **Satu tabel mapping** dari `Error.Type` (Validation→400/`InvalidArgument`, NotFound→404/`NotFound`, Conflict→409/`FailedPrecondition`, Unauthorized→401/`Unauthenticated`, Unexpected→500/`Internal`).
- `Wms.BuildingBlocks.Application`: behavior `LoggingBehavior`, `AuthorizationBehavior` (no-op + `// TODO-AUTH`), `ValidationBehavior` (FluentValidation → `Error(Validation)`, **tanpa throw**), `TransactionBehavior` (command-side via marker `ICommand`; rollback saat `Result.Failure`, bukan hanya exception). Registrasi DI urut **Logging→Authorization→Validation→Transaction→Handler**.
- Refactor slice 01c (Inbound `CreateGoodsReceipt`/`ConfirmGoodsReceipt`, Inventory consumer) → return `Result`; endpoint `Inbound.Api` map via `ToProblemDetails`.
- `tests/Wms.Architecture.Tests`: **FF #7** (no business `throw` di `*.Domain` — grep/Roslyn "minimum viable grep", dilabeli jujur).

**Tasks:**
1. `Error` mapping table + `ToProblemDetails` extension di `Wms.BuildingBlocks.Web` (5 nilai `Error.Type` → status REST).
2. `ResultExceptionInterceptor` (gRPC server interceptor) memetakan `Error.Type` → `StatusCode` lewat tabel yang sama (satu sumber, tak diduplikasi).
3. `LoggingBehavior` (log request name + outcome `IsSuccess`/`Error.Code`) — paling luar.
4. `AuthorizationBehavior` no-op pass-through + `// TODO-AUTH: pipeline-authz` (slot ADR-0004/0012, di-wire Phase 07a).
5. `ValidationBehavior` jalankan `IValidator<T>` → bila gagal kembalikan `Result.Failure(Error(Validation))` **tanpa throw** (short-circuit sebelum Transaction).
6. `TransactionBehavior` scoped ke `ICommand` saja (query skip); buka tx, jalankan handler, **rollback bila `Result.IsFailure`** atau exception, commit bila sukses.
7. Daftarkan keempat behavior berurutan `Logging→Authorization→Validation→Transaction→Handler` di `AddApplication` BuildingBlocks.
8. Refactor handler 01c → signature `Result`/`Result<T>`; pasang `ICommand` di command Inbound; endpoint `Inbound.Api` pakai `ToProblemDetails`.
9. Implement **FF #7** di `Wms.Architecture.Tests` (scan `*.Domain` untuk `throw` di luar guard programmer-error).
10. Behavioral test: command sengaja gagal validasi → `ProblemDetails` (bukan exception) + UoW **rolled back** (tak ada partial write).

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **6 FF + FF #7 = 7 pass**.
- Behavioral test hijau: failing command → RFC 7807 `ProblemDetails` (tanpa exception bocor) **dan** UoW rollback saat `Result.Failure` (state tak ter-commit parsial).
- **E2E 01c tetap hijau** (refactor `Result` tak memecah walking skeleton).

**Learning objective:** Result pattern & no-throw-for-business; RFC 7807 `ProblemDetails`; MediatR pipeline ordering (fail-fast authz/validation sebelum transaksi); Unit-of-Work rollback-on-failure (bukan hanya exception).

**Handoff notes:** Pipeline baseline + `Result`→transport mapping terkunci di BuildingBlocks; slice 01c jadi contoh pemakaian template. **02b** menambah event-contract catalog (`asyncapi.yaml`) + FF #11 + emission policy + DeadLetter baseline di atas pipeline ini. Slot `AuthorizationBehavior` & `AuditLogBehavior` sengaja placeholder — jangan di-wire di sini.

**Touchpoint cert:** AZ-204 — RFC 7807 `ProblemDetails` di ASP.NET Core *(pattern, light — no specific Azure service)* → X. PCD — *no cert touchpoint* (pure error-handling pattern).
