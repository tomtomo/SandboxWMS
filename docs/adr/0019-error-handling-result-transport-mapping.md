# ADR-0019: Error handling — Result pattern, no-throw-for-business, transport mapping & rollback-on-failure

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** `BuildingBlocks.Domain` (Result), `BuildingBlocks.Application` (pipeline), `BuildingBlocks.Web` (ProblemDetails/gRPC interceptor); semua modul

## Context

Seedwork kita sudah punya `Result<T>` ([ADR-0004](0004-cqrs-vertical-slice.md)), tapi tiga hal tak pernah diputuskan: (a) apakah business failure dilempar sebagai exception atau dikembalikan sebagai `Result`; (b) bagaimana failure dipetakan ke transport (REST & gRPC); (c) kapan Unit-of-Work melakukan rollback. Dua footgun nyata: business `throw` lolos jadi **HTTP 500** generik, dan handler yang me-return `Result.Failure` tapi transaksinya **sudah commit** mutasi parsial.

## Decision

- **Pilihan:** Satu kebijakan error-handling terpadu:
  1. **No-throw-for-business** — domain & application mengembalikan `Result`/`Result<T>`; `throw` **direservasi** untuk programmer-error / pelanggaran invariant yang tak boleh terjadi.
  2. **`Error { Code, Message, Type }`** dengan `ErrorType` sebagai satu sumbu 5-nilai: `Validation` · `NotFound` · `Conflict` · `Unauthorized` · `Unexpected`.
  3. **Satu tabel mapping**: REST → RFC 7807 `ProblemDetails` via satu extension; gRPC → `RpcException`/status code via satu interceptor (di `BuildingBlocks.Web`).
  4. `ValidationBehavior` (FluentValidation) memproduksi `Error(Type=Validation)` **tanpa throw**.
  5. `TransactionBehavior`/UoW **rollback saat `Result.Failure`**, bukan hanya saat exception (single-DbContext).
  6. **Fitness function #7**: tak ada business `throw` di `*.Domain` (grep/Roslyn, lihat [ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)).
- **Kenapa:** Result eksplisit membuat failure jadi bagian dari signature (tak tersembunyi sebagai control-flow exception); rollback-on-failure menutup celah partial-commit. `→ Canon: Fowler (PoEAA), Service Layer & error handling; Khononov (LDDD), modeling failure sebagai nilai; MS Learn: ProblemDetails / RFC 7807`.
- **Trade-off:** Disiplin: tiap handler harus memeriksa & merambatkan `Result`; boilerplate map di tepi.
- **Kapan ditinjau ulang:** Bila masuk transaksi terdistribusi (saat ini deferred) → rollback semantics lintas-service butuh saga/kompensasi ([ADR-0005](0005-event-driven-outbox.md)), bukan UoW lokal.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Result + no-throw + rollback-on-failure** *(dipilih)* | Failure eksplisit & testable; partial-commit tertutup; mapping terpusat | Disiplin propagate Result; boilerplate map | Fowler (PoEAA); MS Learn (ProblemDetails) |
| B. Exception untuk alur bisnis | Lebih sedikit kode propagate | Control-flow tersembunyi; mahal; bocor jadi HTTP 500 | Khononov (LDDD) |
| C. Ad-hoc per handler | Cepat awal | Tak konsisten; mapping tersebar; drift | Richards & Ford (Fundamentals) |

## Consequences

**Positif**
- Validasi & business failure punya bentuk seragam yang dipetakan sekali ke REST & gRPC ([ADR-0006](0006-grpc-internal-rest-ui.md)).
- Rollback-on-`Result.Failure` menjaga atomicity write tanpa bergantung pada exception.

**Trade-off / lebih sulit**
- FF #7 versi awal = "minimum viable grep" (jujur dilabeli); Roslyn analyzer jadi roadmap.

**Yang harus dijaga**
- `*.Domain` tetap nol `throw` untuk alur bisnis; `Error.Type` tetap 5 nilai (tambah nilai = keputusan sadar, bukan diam-diam).

## Out of scope / deferred

- Rollback **hanya** single-service/single-DbContext; konsistensi lintas-service tetap eventual ([ADR-0005](0005-event-driven-outbox.md)).
- Roslyn analyzer penuh untuk no-throw (vs grep) di-defer.
