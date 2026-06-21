# ADR-0011: Akses MasterData & Auth via gRPC read-API + cache-aside

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Supporting context MasterData (Warehouse/Location/Product) & Auth (User/Role/Permission); konsumen = core modul

## Context

DB-per-service ([ADR-0010](0010-data-ownership-db-per-service.md)) melarang core modul membaca tabel MasterData/Auth langsung. Namun core butuh data referensi ini **sering** (validasi SKU/location saat scan, lookup user). Datanya **read-heavy, berubah jarang**, dan **read-only** dari sisi core (tak ada koordinasi tulis) — jadi tak butuh domain event untuk sinkronisasi.

## Decision

- **Pilihan:** MasterData & Auth meng-expose **read-API gRPC**; core modul mengaksesnya **synchronous** via kontrak itu (bukan direct table). Karena read-heavy & jarang berubah, hasilnya **di-cache pakai cache-aside** (lazy load, populate on miss, TTL/invalidation).
- **Kenapa:** Read-API menjaga boundary DB-per-service tanpa share schema; sinkron pas karena read-only & butuh data fresh-ish saat dipakai; cache-aside menekan latency & beban authority untuk data yang jarang berubah. `→ Canon: Ford et al. (Hard Parts), data ownership & read patterns terdistribusi; Fowler (PoEAA), caching/identity; Kleppmann (DDIA), read scaling & staleness`.
- **Trade-off:** Cache memunculkan **staleness** (data master berubah, cache belum invalidasi) & temporal coupling sinkron (authority down → caller terdampak; mitigasi: cache + resilience).
- **Kapan ditinjau ulang:** Bila staleness jadi masalah korektнес → tambah event invalidasi dari MasterData; bila sinkron-coupling jadi rapuh → pertimbangkan replikasi read-model via event.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. gRPC read-API + cache-aside** *(dipilih)* | Boundary terjaga; fresh-ish; latency rendah utk read-heavy | Staleness cache; temporal coupling sinkron | Ford et al. (Hard Parts); Fowler (PoEAA) |
| B. Direct table / shared schema | Tercepat, JOIN mudah | Melanggar DB-per-service ([ADR-0010](0010-data-ownership-db-per-service.md)) | Newman (Building Microservices) |
| C. Replikasi penuh via event-carried state transfer | Tak ada sinkron-call di hot path | Infra replikasi & konsistensi data master di tiap konsumen — overkill utk skala ini | Bellemare; Kleppmann (DDIA) |

## Consequences

**Positif**
- Boundary DB-per-service utuh; MasterData/Auth tetap satu source of truth.
- Read-only → tak perlu domain event untuk koordinasi (lebih sederhana dari core write-context).

**Trade-off / lebih sulit**
- Butuh kebijakan cache (TTL, invalidation) & resilience (timeout/retry/circuit-breaker) terhadap authority — port abstrak di BuildingBlocks, implementasi per-cloud ([ADR-0002](0002-tri-cloud-hexagonal.md)).
- Field referensi **kritikal** tetap perlu di-snapshot ke aggregate transaksional ([ADR-0014](0014-snapshot-vs-reference-master-data.md)) — read-API + cache **bukan** pengganti snapshot.

**Yang harus dijaga**
- Akses selalu via kontrak read-API; cache adalah optimasi di sisi konsumen, bukan store kebenaran.

## Out of scope / deferred

- Mekanisme invalidasi berbasis event (push) di-defer; mulai dengan TTL.
- Pemilihan cache store konkret (in-memory vs Redis/Azure Cache vs Memorystore) = deploy-time via adapter.

## Amendment — 2026-06-20

> Read-API + cache-aside di atas tetap (TTL-first tetap default). Blok ini menamai port-nya & **mencatat (bukan mengaktifkan)** jalur invalidasi.

- **`ICacheStore` port** (get/set/remove + TTL) ditambahkan ke named ports ([ADR-0002](0002-tri-cloud-hexagonal.md)): InMemory (Local) + satu adapter StackExchange.Redis untuk Azure Cache for Redis & GCP Memorystore.
- **Event-driven invalidation — DICATAT, tidak diaktifkan**: jalur ter-vetting bila kelak dibutuhkan = inbox handler mengonsumsi integration event `ProductUpdated` di rel Outbox/Inbox yang sudah ada lalu `ICacheStore.RemoveAsync(key)` (nol infra baru). TTL-first tetap berlaku sekarang. **Caveat simetri**: jika diaktifkan, invalidasi **semua** master entity (Warehouse/Location, bukan cuma Product) atau scope secara sadar.
- **gRPC read-port & query filter**: gRPC jalan di Kestrel → `HttpContext` non-null; bila ada global query filter (mis. soft-delete [ADR-0014](0014-snapshot-vs-reference-master-data.md)), bypass harus **filter-name-targeted**, jangan blanket `IgnoreQueryFilters` (akan mematikan soft-delete juga).
