# ADR-0032: Idempotency-Key pada mutating REST endpoint

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-24
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** REST untuk UI/gateway ([ADR-0006](0006-grpc-internal-rest-ui.md)); tabel rail `infrastructure` schema ([ADR-0010](0010-data-ownership-db-per-service.md) amendment); port/adapter Hexagonal ([ADR-0002](0002-tri-cloud-hexagonal.md)); error transport ([ADR-0019](0019-error-handling-result-transport-mapping.md))

## Context

Mutating REST endpoint (POST/PATCH/DELETE) **tak retry-safe lintas-network**: bila response hilang (timeout, koneksi putus) setelah server memproses, client tak bisa membedakan "belum diproses" vs "sudah diproses, response hilang". Retry buta → operasi ganda **atau** client tak bisa mengambil hasil sukses yang sah. Guard state-machine domain melindungi **correctness** sebagian (mis. `Wave.MarkReady` idempotent), tapi tak menyediakan **kontrak retry-safety di layer API** (client tak dapat me-replay response sukses asli).

Ini **independen** dari Inbox idempotency broker-level ([ADR-0005](0005-event-driven-outbox.md)): Inbox melindungi consumer event dari duplikat broker; ini melindungi **caller HTTP** dari duplikat request. EIP **Idempotent Receiver** di tingkat API. ✔ grep `Idempotency-Key` di `src` = 0.

## Decision

- **Pilihan:** Adopsi **`Idempotency-Key` header (RFC/IETF draft, pola Stripe)** untuk mutating REST. Middleware (`UseIdempotencyKey()`, early di pipeline setelah `UseCorrelationId`, sebelum `UseAuthentication`) untuk request mutating yang membawa header `Idempotency-Key`:
  1. Cari `(endpoint, key)` di store (`IApiIdempotencyStore`). **HIT** → replay status + body tersimpan, **tanpa** mengeksekusi handler. **MISS** → eksekusi handler, capture response, simpan, lalu kirim ke client.
  2. Store = tabel **`infrastructure.api_idempotency`** (`endpoint`, `idempotency_key`, `status_code`, `response_body`, `recorded_at`, `traceparent`) — ko-lokasi di DB tiap service (sama pola `audit_log`/`dead_letter`, [ADR-0010](0010-data-ownership-db-per-service.md) amendment), port di `BuildingBlocks.Application`, adapter di `Platform.<cloud>` (Hexagonal). TTL ~24h.
  - **Keputusan desain (resolved):** **(a) Key scope GLOBAL** per `(endpoint, key)` — client bertanggung jawab atas keunikan key (RFC 9110 guidance); isolasi per-user di-defer ke authZ ([ADR-0012](0012-deferred-authorization-enforcement.md)). **(b) Store SYNCHRONOUS dalam request scope** (bukan fire-and-forget — DbContext scoped akan ter-dispose), **best-effort**: gagal-simpan di-log, TAK menggagalkan response yang sudah dirender (retry berikutnya MISS → re-eksekusi, aman). **(c) Replay status + JSON body**; body > cap (mis. 8 KB) → tak di-cache (re-eksekusi saat retry); endpoint stream/biner tak didukung (JSON-only). **(d) Endpoint identity** = `"{METHOD} {path}"` (path konkret). **(e) In-flight duplicate** (dua request key sama paralel sebelum yang pertama commit): composite PK `(endpoint, idempotency_key)` → INSERT kedua konflik → di-serap (request itu tetap melayani response-nya sendiri); serialisasi ketat (distributed lock) di-defer. **(f) TTL cleanup** = tanggung jawab adapter/ops (Postgres job/trigger `DELETE WHERE recorded_at < now()-24h`); Local **tak** auto-cleanup (di-flag — tabel tumbuh sampai job dijalankan).
- **Kenapa:** Retry-safety adalah kontrak HTTP standar untuk mutating ops; mengangkatnya ke middleware-level menjadikannya cross-cutting (tiap host opt-in via satu `UseIdempotencyKey()`) tanpa menyebar ke tiap handler. Store di-port-kan supaya adapter cloud (Redis/Memorystore dengan TTL native) menggantikan Postgres tanpa menyentuh middleware. `→ Canon: Hohpe & Woolf (EIP), Idempotent Receiver; IETF "The Idempotency-Key HTTP Header Field" (draft); Stripe idempotency; RFC 9110 (idempotent methods & client key responsibility).`
- **Trade-off:** Satu write per first-time mutating request + tabel infra baru (**7 migration**, satu per module DbContext — `api_idempotency` ditambah ke `AddInfrastructureTables`). Response-buffering middleware = surface intrusif (JSON-only, cap ukuran). TTL cleanup tak otomatis di Local (ops concern). **Value rendah untuk sandbox single-user** (diakui plan) — diadopsi sebagai **pattern-learning** (enterprise retry-safety) + pre-position multi-client masa depan.
- **Kapan ditinjau ulang:** Saat deploy multi-replica/cloud → adapter Redis/Memorystore (TTL native, distributed) menggantikan Local Postgres; bila butuh strict-serialization in-flight → tambah distributed lock; bila response besar/biner jadi umum → claim-check atau exclude.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. `Idempotency-Key` middleware + `infrastructure.api_idempotency` store (port/adapter)** *(dipilih)* | Kontrak HTTP standar; cross-cutting (1 wiring/host); port → swap adapter cloud TTL-native | Response-buffering intrusif; 7 migration; TTL cleanup manual di Local | Hohpe & Woolf (EIP); IETF Idempotency-Key; Stripe |
| B. Andalkan guard state-machine domain saja | Nol kerja; correctness sebagian terjaga | Client tak bisa replay response sukses; bukan kontrak retry HTTP | — |
| C. Idempotency per-handler (bukan middleware) | Granular | Menyebar concern ke tiap handler; bukan cross-cutting; duplikasi | — |

## Consequences

**Positif**
- Mutating REST retry-safe: client retry dengan key sama → response asli di-replay (bukan operasi ganda / 409 menyesatkan).
- Port `IApiIdempotencyStore` → adapter cloud (Redis TTL-native) tanpa sentuh middleware/core (Hexagonal).
- Latihan konkret pola enterprise idempotency (Stripe/EIP).

**Trade-off / lebih sulit**
- 7 migration (satu per module DbContext); response-buffering JSON-only + cap ukuran; TTL cleanup = ops job (Local tak otomatis → tabel tumbuh tanpa job).
- Store best-effort: window non-atomic (response terkirim, simpan gagal → retry re-eksekusi) — diterima sadar.

**Yang harus dijaga**
- Store **synchronous dalam request scope** (jangan fire-and-forget — DbContext ter-dispose).
- Hanya method mutating + ada header `Idempotency-Key`; GET & request tanpa header pass-through nol-biaya.
- Composite PK `(endpoint, idempotency_key)` = pengaman in-flight; JSON/text-only; cap ukuran response.

## Out of scope / deferred

- TTL cleanup otomatis (Postgres job/trigger; Redis TTL native) → adapter/ops, di-flag.
- Per-user/tenant key isolation → saat authZ aktif ([ADR-0012](0012-deferred-authorization-enforcement.md), 07a).
- Strict in-flight serialization (distributed lock) → multi-replica cloud (05b/06b).
- Response biner/stream idempotency → claim-check (EIP) bila perlu.
