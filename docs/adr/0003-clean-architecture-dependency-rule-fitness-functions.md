# ADR-0003: Clean Architecture + Dependency Rule ditegakkan 6 fitness function

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Lintas-cutting — `BuildingBlocks.*`, semua modul, `Platform.*`, `tests/<App>.Architecture.Tests`

## Context

Boundary arsitektur yang hanya hidup di konvensi/dokumen akan **luntur** seiring waktu (entropy). Sistem ini punya banyak aturan arah-dependensi: layer menuju Domain, modul tak saling menyentuh internal, SDK cloud terkurung. Tanpa penegakan otomatis, drift tak terdeteksi sampai mahal diperbaiki.

## Decision

- **Pilihan:** Adopsi **Clean Architecture Dependency Rule** (dependensi source code hanya menuju Domain) sebagai hukum struktur, dan **tegakkan dengan 6 fitness function** (NetArchTest) yang **fail build** saat dilanggar.
- **Kenapa:** Architecture-as-code: aturan yang bisa dieksekusi tak bisa diabaikan. Fitness function mengubah governance arsitektur dari review manual jadi gate CI otomatis. `→ Canon: Martin (Clean Architecture), Dependency Rule; Richards & Ford (Fundamentals), fitness functions & architecture governance; Ford et al. (Evolutionary Architecture), istilah fitness function`.
- **Trade-off:** Suite test arsitektur perlu dirawat; aturan terlalu ketat bisa menghambat refactor sah — tiap aturan harus punya alasan.
- **Kapan ditinjau ulang:** Saat sebuah aturan sering di-suppress secara sah → tanda aturan itu salah model, bukan kodenya.

**6 fitness function (peta otoritatif):**
1. Nol SDK cloud (`Azure.*`/`Google.*`/`Amazon.*`) di `Modules.*` & `BuildingBlocks.*`.
2. `*.Domain` nol framework (no EF / mediator / ASP.NET).
3. Modul tak me-reference Domain/Application/Infrastructure modul lain — hanya `*.Contracts`/`*.Grpc`.
4. `BuildingBlocks` tak me-reference `Modules`/`Platform`.
5. Dependency rule intra-modul: Domain ⊁ Application/Infrastructure/Api; Application ⊁ Infrastructure/Api.
6. `Platform.*` tak me-reference `Modules.*`.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Dependency Rule + fitness function otomatis** *(dipilih)* | Boundary self-enforcing; drift ketahuan di CI | Perlu rawat suite arsitektur | Martin (Clean Architecture); Richards & Ford (Fundamentals) |
| B. Dependency Rule via konvensi + code review | Nol kode test tambahan | Luntur tanpa penegakan; bergantung disiplin manusia | Ford et al. (Evolutionary Architecture) |
| C. Enforce via project reference saja (tanpa test) | Compiler tolak sebagian pelanggaran | Tak menangkap aturan semantik (mis. SDK leakage transitif) | — |

## Consequences

**Positif**
- Keenam ADR struktural lain ([ADR-0001](0001-microservices-from-start.md), [0002](0002-tri-cloud-hexagonal.md), [0009](0009-contracts-vs-grpc-separation.md), [0010](0010-data-ownership-db-per-service.md)) punya penjaga eksekusi — bukan sekadar niat.
- Split monorepo→polyrepo ([ADR-0007](0007-monorepo-with-polyrepo-path.md)) jadi murah karena #3 menjamin tak ada coupling internal antar-modul.

**Trade-off / lebih sulit**
- Build merah saat aturan dilanggar bisa terasa menghambat di awal — itu memang tujuannya.

**Yang harus dijaga**
- `Architecture.Tests` harus hijau sebelum merge; aturan baru ditambah saat boundary baru lahir.

## Out of scope / deferred

- Fitness function runtime/operasional (latency budget, coupling metric live) belum di-scope — keenam ini fokus struktur statis.

## Amendment — 2026-06-20

> Enam fitness function inti di atas tetap (dan tetap disebut "6 inti"). Blok ini menambah FF yang **co-located** di ADR yang mereka jaga — bukan ditumpuk ulang di sini — plus membedakan FF **statik** (NetArchTest) vs **behavioral** (test perilaku; NetArchTest tak bisa introspeksi body method).

**Registry FF tambahan (analisis statik — NetArchTest *kecuali ditandai*):**
7. Tak ada business `throw` di `*.Domain` — via grep → Roslyn (**bukan** NetArchTest: ia tak bisa introspeksi body method) — [ADR-0019](0019-error-handling-result-transport-mapping.md).
8. gRPC service (`*.Api`) tak me-reference `DbContext` langsung (reader-delegation via read-port) — NetArchTest — [ADR-0010](0010-data-ownership-db-per-service.md).
9. Tak ada `Local*` adapter / token-provider di-reference oleh cloud host — NetArchTest — [ADR-0021](0021-service-to-service-auth.md).
10. Tak ada `InfrastructureDbContext` standalone; tabel infra dimiliki DbContext tiap modul — NetArchTest — [ADR-0010](0010-data-ownership-db-per-service.md).
11. Contract-coverage: tiap tipe `*.Contracts` yang dipublish punya channel di `asyncapi.yaml` (directional) — **test statik/reflection yang mem-parse YAML** (bukan NetArchTest murni; tetap reuse harness) — [ADR-0023](0023-event-contract-catalog-asyncapi.md).

**Behavioral (test suite runtime, BUKAN NetArchTest):**
- Negative-security: token `alg:none` / wrong-aud / unsigned ditolak — [ADR-0016](0016-refresh-token-rotation.md), [ADR-0021](0021-service-to-service-auth.md).
- Aggregate-emission: method aggregate me-raise domain event-nya — [ADR-0026](0026-tactical-ddd-conventions.md).
- Split-timeout-configured: timeout gRPC ≠ HTTP & keduanya ter-set — [ADR-0020](0020-resilience-pipeline-defaults.md).
