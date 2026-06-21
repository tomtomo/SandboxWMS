# ADR-0023: Authoritative event-contract catalog (AsyncAPI) + contract-coverage fitness function

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** `docs/architecture/asyncapi.yaml`; `*.Contracts` ([ADR-0009](0009-contracts-vs-grpc-separation.md)); contract-coverage FF **didefinisikan di ADR ini**, terdaftar di registry [ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)

## Context

Hari ini kontrak integration-event kita **implicit di tipe CLR** dan tersebar di ADR-0005/0011/0017 — tak ada satu artefak otoritatif yang menyatakan emitter → receiver → trigger. Akibatnya seam EDA sulit dibaca, dan tak ada penjaga yang memastikan tiap event yang dipublish punya konsumen terdokumentasi (atau sengaja ditandai unconsumed). Juga: nama broker-facing event terikat ke nama kelas CLR → rename kelas = breaking change diam-diam.

## Decision

- **Pilihan:** Satu **`asyncapi.yaml` (AsyncAPI 3.0)** in-source, git-versioned & PR-reviewed, sebagai **kontrak otoritatif** cross-context integration-event: matriks emitter → receiver → trigger + kolom **emitted-but-unconsumed** yang ditandai. **Spec-wins** sebagai drift rule.
  - **Logical event identity** `{module}.{event_snake}.v{N}` sebagai nama broker-facing yang stabil, **decoupled dari tipe CLR**, di-bind via attribute/const statik per contract.
  - **SemVer bump rule**: tambah field nullable = non-breaking (tetap `vN`); remove/rename/retype/tighten = breaking → `v{N+1}`.
  - **Satu contract-coverage fitness function** — *static/reflection-based test* yang mem-parse `asyncapi.yaml` lalu reflect atas tipe `*.Contracts` (BUKAN NetArchTest murni — ia membaca artefak YAML eksternal, di luar kapabilitas type-dependency NetArchTest; tetap reuse harness test [ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)): tiap tipe `*.Contracts` yang dipublish menurunkan logical name-nya dan **wajib punya channel terdeklarasi** (directional only).
- **Kenapa:** Governance kontrak yang **executable** di atas Published Language yang sudah kita commit ([ADR-0009](0009-contracts-vs-grpc-separation.md)); git/provider-neutral cocok dengan zero-SDK core ([ADR-0002](0002-tri-cloud-hexagonal.md)) — tak ada otoritas schema runtime cloud. `→ Canon: Bellemare (Event-Driven Microservices), event schema & evolution; Hohpe & Woolf (EIP), Published Language; Ford et al. (Hard Parts), distributed contracts; spec: AsyncAPI 3.0`.
- **Trade-off:** Artefak yang harus dijaga sinkron dengan kode (mitigasi: FF directional); FF awal hanya satu arah.
- **Kapan ditinjau ulang:** Bila butuh schema enforcement runtime / konsumen eksternal → cloud schema registry (deferred seam).

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Satu AsyncAPI in-source + 1 contract-coverage FF + logical identity/SemVer** *(dipilih)* | Otoritatif, executable, provider-neutral; reuse harness | Jaga sync doc↔kode; FF satu arah | Bellemare; Hohpe & Woolf (EIP); AsyncAPI 3.0 |
| B. Kontrak implicit di tipe CLR (status quo) | Nol artefak | Seam tak terbaca; rename = breaking diam-diam | Ford et al. (Hard Parts) |
| C. Schema registry / Pact sekarang | Enforcement kuat | Infra & toolchain berat; keduanya deferred | Bellemare |

## Consequences

**Positif**
- Satu sumber kebenaran seam EDA ([ADR-0005](0005-event-driven-outbox.md)); event unconsumed (StockLow/StockNearExpiry/StockQuarantineStale) terlihat sebagai gap eksplisit.
- Logical identity melindungi dari breaking change akibat rename kelas; SemVer rule mengisi "event-versioning detail" yang sebelumnya deferred ([ADR-0009](0009-contracts-vs-grpc-separation.md)).

**Trade-off / lebih sulit**
- Reverse-coverage (tiap receiver punya emitter) = known gap terdokumentasi (hormati Reporting config-driven [ADR-0017](0017-eventual-consistency-reporting-notification.md)).

**Yang harus dijaga**
- FF contract-coverage hijau sebelum merge; `asyncapi.yaml` di-PR-review seperti kode.

## Out of scope / deferred

- AsyncAPI CLI validate sebagai CI gate (narik toolchain Node) → ditolak; cukup FF NetArchTest untuk drift semantik.
- Cloud schema registry & consumer-driven contract test (Pact) → tetap deferred.
- Producer multi-publish / per-version deprecation machinery → ditolak (over-engineering untuk solo).
