# ADR-0020: Resilience pipeline defaults (Polly v8) dengan split per-transport timeout

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** `BuildingBlocks.Infrastructure` (resilience factory); call gRPC/HTTP/outbox-dispatch; Polly dipin via CPM ([ADR-0007](0007-monorepo-with-polyrepo-path.md))

## Context

Integration point adalah sumber kegagalan paling umum di sistem terdistribusi. Blueprint sudah menyebut "resilience" di `BuildingBlocks.Infrastructure` tapi **tak ada keputusan konkret** soal timeout/retry/circuit-breaker. Bahaya khusus untuk kita: compute Mixed PaaS ([ADR-0018](0018-compute-hosting-mixed-paas.md)) memakai **scale-to-zero** — call gRPC pertama ke target yang dingin bisa makan beberapa detik (cold start).

## Decision

- **Pilihan:** Satu **`ResiliencePipelineDefaults` factory** (Polly v8) di `BuildingBlocks.Infrastructure` sebagai single source of truth untuk gRPC, HTTP, dan outbox-dispatch.
  - Default-on per named `HttpClient` (`ConfigureHttpClientDefaults` + opt-out eksplisit); gRPC konsumsi via `ResiliencePipelineProvider<string>` per-client key.
  - Urutan strategi: **Timeout (innermost, per-attempt) → Retry → CircuitBreaker**, dengan predikat per-transport.
  - **Split timeout**: gRPC ~**30s** (toleran cold-start) vs HTTP ~**5s** (fail-fast).
  - Tabel knob terkunci (ubah = ubah ADR). Satu **fitness function behavioral** `split-timeout-configured` (test yang memastikan timeout gRPC ≠ HTTP & keduanya ter-set) — *behavioral* (inspeksi nilai config runtime), **bukan** NetArchTest; terdaftar di registry [ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md).
- **Kenapa:** Stability patterns kanonik di satu tempat; **split timeout** adalah insight load-bearing — timeout 5s seragam akan menggagalkan call gRPC pertama ke service scale-to-zero pada attempt-1, alih-alih menyerap cold start. `→ Canon: Nygard (Release It!), Timeout/Retry/Circuit Breaker/Bulkhead; MS Learn: Polly v8 resilience pipelines`.
- **Trade-off:** Angka default (30s/5s/retry count/break ratio) bersifat provisional sampai ada traffic nyata; retry harus dipasangkan idempotency untuk operasi non-read.
- **Kapan ditinjau ulang:** Saat ada metrik produksi nyata → kalibrasi angka; saat ada saturasi konkurensi terbukti → baru pertimbangkan bulkhead/rate-limit.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Satu factory, Timeout→Retry→CB, split timeout** *(dipilih)* | Default konsisten; cold-start absorbed; cert-relevant | Angka provisional; retry butuh idempotency | Nygard (Release It!); MS Learn (Polly v8) |
| B. Timeout seragam semua transport | Lebih sederhana | gRPC ke scale-to-zero gagal di attempt-1 | Nygard (Release It!) |
| C. Per-callsite ad-hoc resilience | Fleksibel | Tak konsisten; mudah lupa; drift | Richards & Ford (Fundamentals) |

## Consequences

**Positif**
- Semua call keluar punya baseline stability seragam; cold-start ([ADR-0018](0018-compute-hosting-mixed-paas.md)) tertangani by default.
- Read-API MasterData/Auth ([ADR-0011](0011-master-data-read-api-cache-aside.md)) langsung dapat timeout/retry/CB.

**Trade-off / lebih sulit**
- Retry pada operasi yang punya efek samping menuntut idempotency (Inbox, [ADR-0005](0005-event-driven-outbox.md)).
- Per-callsite override record diperlukan untuk edge case (mis. upload blob besar, [ADR-0015](0015-grattachment-aggregate-object-storage.md)).

**Yang harus dijaga**
- Polly v8 ditambahkan ke Central Package Management ([ADR-0007](0007-monorepo-with-polyrepo-path.md)); factory tetap satu-satunya sumber konfigurasi.

## Out of scope / deferred

- Bulkhead & rate-limit — tanpa kebutuhan saturasi konkurensi nyata, tak diadopsi sekarang → catatan future-evolution.
- Kalibrasi angka berdasarkan load test → deferred sampai ada environment dengan traffic.
