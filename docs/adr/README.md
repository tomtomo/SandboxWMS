# Architecture Decision Records — TomSandboxWMS

Index dari **Architecture Decision Record (ADR)** untuk sandbox microservices **TomSandboxWMS**. Tiap ADR memformalkan **satu keputusan** yang sudah final & terkunci, agar jejaknya **auditable**: konteks, keputusan, alternatif yang ditolak, konsekuensi (positif + trade-off), dan ankor ke sumber kanonik.

> ADR ini adalah **formalisasi** dari dokumen acuan arsitektur & domain — bukan brainstorming baru. Arsitektur sudah diputuskan; di sini ia dicatat secara tegas & ber-attribution.

## Konvensi

- **Penomoran** urut & immutable: `NNNN-<slug>.md`. Nomor tak pernah dipakai ulang.
- **Status lifecycle:** `Proposed → Accepted → Superseded by ADR-XXXX | Deprecated`. Semua ADR di set awal ini **Accepted**.
- **Immutable setelah Accepted.** Perubahan keputusan = **ADR baru** yang men-*supersede* yang lama. **Pengecualian (in-flight):** selama set ini belum teruji implementasi, penambahan nuansa dari pass review ditulis sebagai blok **`## Amendment — <tanggal>`** di akhir file — prosa keputusan asli **tak diubah**. Setelah battle-tested, kembali ke aturan supersede penuh.
- **Format** tiap ADR mengikuti `assets/adr-template.md` (skill `enterprise-app-dev`): Context · Decision (Pilihan/Kenapa+canon/Trade-off/Kapan ditinjau) · Options considered · Consequences · Out of scope.
- **Tiga tier:** **A** = struktural (blueprint) · **B** = domain/infra (overview WMS + compute) · **C** = cross-cutting & operasional.

## Tier A — Keputusan Struktural

> Sumber: [`tri-cloud-microservices-blueprint.md`](../architecture/tri-cloud-microservices-blueprint.md). Reusable lintas-app (WMS, CRP, HRMS, …).

| # | Keputusan | Status | Inti |
|---|---|---|---|
| [0001](0001-microservices-from-start.md) | Microservices sejak awal | Accepted | Distribusi penuh sejak hari-1 — *deviasi sadar* dari default modular-monolith, dibenarkan tujuan belajar |
| [0002](0002-tri-cloud-hexagonal.md) | Tri-cloud via Hexagonal | Accepted | Core nol cloud SDK; adapter + host per-cloud; cloud ke-N tanpa rombak core |
| [0003](0003-clean-architecture-dependency-rule-fitness-functions.md) | Clean Arch + 6 fitness function | Accepted | Dependency Rule ditegakkan NetArchTest yang fail build |
| [0004](0004-cqrs-vertical-slice.md) | CQRS + Vertical Slice | Accepted | Write lewat aggregate; read bypass ke DTO; Application per use-case |
| [0005](0005-event-driven-outbox.md) | EDA: domain event → Outbox → broker | Accepted | Integration event ber-versi, atomicity via Outbox, ACL di consumer |
| [0006](0006-grpc-internal-rest-ui.md) | gRPC antar-service, REST untuk UI | Accepted | Protokol tepat-guna per kanal; gateway managed tanpa transcoding |
| [0007](0007-monorepo-with-polyrepo-path.md) | Monorepo + jalur polyrepo | Accepted | Monorepo dulu; split jadi murah berkat fitness function #3 |
| [0008](0008-aspire-distributed-local.md) | Aspire distributed-local | Accepted | Orkestrasi lokal (AppHost) + service defaults; produksi tetap IaC |
| [0009](0009-contracts-vs-grpc-separation.md) | `*.Contracts` (POCO) vs `*.Grpc` | Accepted | Event dependency-free dipisah dari stub gRPC; domain type tak pernah jadi wire |
| [0010](0010-data-ownership-db-per-service.md) | Data ownership — DB-per-service | Accepted | Tiap service punya datanya; no cross-store read; lintas-context via event/kontrak |

## Tier B — Keputusan Domain / Infra

> Sumber: [`tomsandboxwms-overview.md`](../tomsandboxwms-overview.md). Keputusan compute kini **self-contained** di [ADR-0018](0018-compute-hosting-mixed-paas.md).

| # | Keputusan | Status | Inti |
|---|---|---|---|
| [0011](0011-master-data-read-api-cache-aside.md) | MasterData/Auth: gRPC read-API + cache-aside | Accepted | Baca data referensi lintas-service via kontrak sinkron + cache, jaga DB-per-service |
| [0012](0012-deferred-authorization-enforcement.md) | Deferred authorization enforcement | Accepted | AuthN aktif (audit jalan); authZ ditunda via planning catalog + `TODO-AUTH` |
| [0013](0013-two-axis-discrepancy-inbound.md) | Two-axis discrepancy (Inbound) | Accepted | `lineStatus` × `quantityVariance` sebagai dua sumbu independen |
| [0014](0014-snapshot-vs-reference-master-data.md) | Snapshot vs reference master data | Accepted | Field kritikal di-snapshot ke dokumen; non-kritikal reference; soft-delete only |
| [0015](0015-grattachment-aggregate-object-storage.md) | GRAttachment aggregate + object storage | Accepted | Aggregate terpisah; byte di object storage, metadata + blobPath di row |
| [0016](0016-refresh-token-rotation.md) | Refresh-token rotation | Accepted | Hash-only, rotation chain, replay-detection cascade revoke |
| [0017](0017-eventual-consistency-reporting-notification.md) | Eventual consistency Reporting/Notification | Accepted | Pure consumer; projection rebuild-able; notif async + retry/DLQ |
| [0018](0018-compute-hosting-mixed-paas.md) | Compute Hosting Model — Mixed PaaS | Accepted | Compute profil-driven (container/web-PaaS/serverless) demi cert breadth — formalisasi semi-ADR |
| [0028](0028-picking-completed-event.md) | Picking-completed event (Outbound→Inventory) | Accepted | Realisasi kanal sinyal overview §B (`Stock Allocated→Picked`); event ke-5 di luar katalog 4, dijaga [ADR-0023](0023-event-contract-catalog-asyncapi.md) |
| [0030](0030-reporting-event-enrichment.md) | Event enrichment + Inventory stock-level events untuk Reporting | Accepted | Event-carried state transfer untuk read-side: enrich `gr_confirmed`(+supplier)/`picking_completed`(+operator) non-breaking + 2 event baru `inventory.putaway_completed.v1`/`inventory.stock_removed.v1` (owner-emitted); katalog 5→7, dijaga [ADR-0023](0023-event-contract-catalog-asyncapi.md) |
| [0034](0034-allocation-failure-signaling.md) | Allocation-failure signaling (stock short) | Accepted | Event eksplisit `inventory.stock_allocation_failed.v1` saat wave allocation tak terpenuhi (ganti silent-drop); Outbound tandai `OrderLine` Short/Backordered + Notification alert; pola eShop `OrderStockRejected`, bukan sync ATP-gate; katalog 7→8, dijaga [ADR-0023](0023-event-contract-catalog-asyncapi.md) |

## Tier C — Cross-cutting & Operasional

> **Tier C** menambah keputusan **cross-cutting & operasional** yang melengkapi Tier A/B: error-handling, resilience, service-to-service auth, audit, kontrak event, observability, dan port lintas-cutting. Sebagian juga **memperkaya ADR existing** lewat blok `## Amendment` (daftar di bawah). Prinsipnya tetap anti-overengineering: pola berat — saga-engine yang dibangun dini, hosting seragam dipaksakan ke semua service, generic projection store lintas-NoSQL, external IdP penuh — **sengaja TIDAK diadopsi** (di-defer / di luar scope) sampai ada pemicu nyata.

| # | Keputusan | Status | Inti |
|---|---|---|---|
| [0019](0019-error-handling-result-transport-mapping.md) | Error handling — Result→transport, no-throw, rollback-on-failure | Accepted | `Result` = failure eksplisit; map ke ProblemDetails/gRPC; rollback saat `Result.Failure` |
| [0020](0020-resilience-pipeline-defaults.md) | Resilience pipeline defaults (Polly v8) | Accepted | Satu factory; **split timeout** gRPC 30s vs HTTP 5s (cold-start) |
| [0021](0021-service-to-service-auth.md) | Service-to-service auth | Accepted | Bearer audience-scoped via `IServiceTokenProvider`; MI/SA OIDC; offline-validate |
| [0022](0022-operational-audit-log.md) | Operational audit log | Accepted | Append-only, outcome-aware, **out-of-band** (survive rollback) |
| [0023](0023-event-contract-catalog-asyncapi.md) | Event-contract catalog (AsyncAPI) + contract-coverage FF | Accepted | `asyncapi.yaml` otoritatif + logical event identity + SemVer |
| [0024](0024-cross-broker-trace-context-propagation.md) | Cross-broker trace-context propagation | Accepted | W3C `traceparent` di envelope; trace utuh producer→consumer |
| [0025](0025-cross-cutting-platform-ports.md) | Cross-cutting platform ports | Accepted | `IDelayedTaskQueue` + `ITelemetryStream`; taksonomi keandalan |
| [0026](0026-tactical-ddd-conventions.md) | Tactical DDD conventions | Accepted | Identity / emission policy / VO ownership + behavioral FF |
| [0027](0027-system-actor-convention.md) | SYSTEM actor convention | Accepted | `HttpContext==null`→SYSTEM; invariant anon≠SYSTEM |
| [0029](0029-local-in-process-messaging-delivery.md) | Local in-proc messaging + deferred cross-process | Accepted | Walking-skeleton E2E via test 1-proses; lintas-proses → broker cloud (P05/06) |
| [0031](0031-optimistic-concurrency-token-xmin.md) | Optimistic concurrency token (`xmin`) pada aggregate root | Accepted | `UseXminAsConcurrencyToken()` tiap aggregate root + `RefreshToken`; `DbUpdateConcurrencyException`→`Error.Conflict` (409); tutup rotation-fork [ADR-0016](0016-refresh-token-rotation.md) + lost-update; zero-schema-cost |
| [0032](0032-idempotency-key-mutating-rest.md) | Idempotency-Key pada mutating REST endpoint | Accepted | Middleware `Idempotency-Key` (RFC/Stripe) → store `infrastructure.api_idempotency` (port/adapter) replay response; retry-safe; key global, store synchronous best-effort, TTL 24h (cleanup = ops) |
| [0033](0033-authentication-security-event-auditing.md) | Auditing event autentikasi & keamanan | Accepted | Login/Refresh/Logout = `IAuditableCommand`; reuse-detection ter-audit out-of-band meski Failure; kredensial auto-redaksi; reuse `AuditLogBehavior` [ADR-0022](0022-operational-audit-log.md) (OWASP ASVS V7) |

**ADR existing yang diperkaya blok `## Amendment — 2026-06-20`**: [0002](0002-tri-cloud-hexagonal.md) named ports · [0003](0003-clean-architecture-dependency-rule-fitness-functions.md) FF 7–11 + behavioral · [0004](0004-cqrs-vertical-slice.md) pipeline ordering · [0005](0005-event-driven-outbox.md) saga rule + composite inbox key + logical event id · [0007](0007-monorepo-with-polyrepo-path.md) CPM · [0010](0010-data-ownership-db-per-service.md) infra-table ownership + MigrationRunner + dead-letter + gRPC reader-delegation (FF #8) · [0011](0011-master-data-read-api-cache-aside.md) `ICacheStore` + event-invalidation note · [0012](0012-deferred-authorization-enforcement.md) `IsActive` filter + warehouse-scoping concept + offline-validation · [0014](0014-snapshot-vs-reference-master-data.md) targeted bypass · [0016](0016-refresh-token-rotation.md) password hashing + RS256 · [0017](0017-eventual-consistency-reporting-notification.md) projection atomicity.

## Peta dependensi keputusan

Bagaimana ADR saling mengunci (panah = "berlandaskan / dijaga oleh"):

```
0001 microservices ─┬─ 0010 DB-per-service ─── 0011 read-API + cache ─── 0014 snapshot
                    ├─ 0005 EDA/Outbox ─────── 0017 eventual consistency (Reporting/Notif)
                    └─ 0002 hexagonal ───────── 0018 compute Mixed PaaS
0003 fitness functions ── menegakkan ──▶ 0001 · 0002 · 0009 · 0010
0006 gRPC/REST ── memakai kontrak ──▶ 0009 Contracts vs Grpc
0004 CQRS+slice ── read-side ──▶ 0017      0007 monorepo ── split murah berkat ──▶ 0003 (#3)
Domain (overview): 0013 two-axis · 0015 GRAttachment · 0016 refresh-token · 0012 deferred-authz
Tier C: 0019 error-handling · 0020 resilience · 0021 s2s-auth · 0022 audit-log
                        0023 asyncapi-catalog(+FF) · 0024 trace-context · 0025 platform-ports · 0026 ddd-conv · 0027 system-actor
0023 contract-coverage FF ── menumpang harness ──▶ 0003      0021/0022/0027 ── dipakai ──▶ audit & s2s identity
0028 picking-completed event ── realisasi sinyal overview §B ──▶ Stock Allocated→Picked; berlandas 0005·0010, dijaga 0023
0030 reporting event enrichment ── event-carried state transfer ──▶ projection §F (Reporting); berlandas 0005·0010·0017, pola 0028, dijaga 0023(FF#11)
0034 allocation-failure signaling ── compensating-signal event ──▶ Outbound line Short/Backorder + Notification; ganti silent-drop overview §B#2; berlandas 0005·0010·0017, pola 0028·0030, dijaga 0023(FF#11)
0029 local in-proc messaging ── E2E via test 1-proses; cross-process ──▶ broker cloud P05/06; berlandas 0005·0008·0010
0031 optimistic concurrency (xmin) ── Optimistic Offline Lock ──▶ aggregate root + RefreshToken; tutup rotation-fork 0016, lost-update; map exception→Conflict 0019; berlandas 0010·0026
0032 idempotency-key ── EIP Idempotent Receiver (API) ──▶ mutating REST middleware + api_idempotency store (port/adapter); retry-safe; berlandas 0006·0010·0002
0033 auth-event auditing ── opt-in IAuditableCommand ──▶ Login/Refresh/Logout; reuse-detection forensik; reuse AuditLogBehavior 0022 (OWASP ASVS V7); berlandas 0016·0027
```

## Legenda canon (ankor)

Singkatan yang dipakai di baris **Kenapa** & kolom **Ankor** tiap ADR — detail lengkap (edisi, "kapan dirujuk") ada di bibliografi beranotasi skill `enterprise-app-dev` (`references/bibliography.md`):

| Ankor | Sumber |
|---|---|
| Evans (DDD) | *Domain-Driven Design* — Eric Evans |
| Vernon (IDDD) | *Implementing Domain-Driven Design* — Vaughn Vernon |
| Martin (Clean Architecture) | *Clean Architecture* — Robert C. Martin |
| Hombergs (GYHDoCA) | *Get Your Hands Dirty on Clean Architecture* — Tom Hombergs |
| Cockburn (Hexagonal) | Ports & Adapters — Alistair Cockburn |
| Richards & Ford (Fundamentals) | *Fundamentals of Software Architecture* |
| Ford et al. (Hard Parts) | *Software Architecture: The Hard Parts* |
| Newman (Building Microservices) | *Building Microservices* 2e — Sam Newman |
| Hohpe & Woolf (EIP) | *Enterprise Integration Patterns* |
| Bellemare | *Building Event-Driven Microservices* |
| Kleppmann (DDIA) | *Designing Data-Intensive Applications* |
| Nygard (Release It!) | *Release It!* 2e |
| Fowler (PoEAA) | *Patterns of Enterprise Application Architecture* (Service Layer, Unit of Work) |
| Fowler (Analysis Patterns) | *Analysis Patterns* — Audit Log |
| Richardson (Microservices Patterns) | Saga (orchestration vs choreography) |
| OWASP | Refresh Token Rotation · Session Management · ASVS · JWT/JWS validation |
| Saltzer & Schroeder | *The Protection of Information in Computer Systems* — complete mediation, least privilege |
| W3C Trace Context | spec propagasi `traceparent`/`tracestate` (OpenTelemetry) |
| AsyncAPI 3.0 | spec kontrak event-driven (di luar bibliografi skill) |

## Menambah ADR baru

1. Ambil nomor urut berikutnya (`0028`, …), slug deskriptif.
2. Salin `assets/adr-template.md` → isi semua section, sertakan ankor canon.
3. Bila men-*supersede* ADR lama: set status ADR lama → `Superseded by ADR-00NN`, jangan hapus.
4. Tambahkan barisnya ke tabel tier yang sesuai di README ini.
