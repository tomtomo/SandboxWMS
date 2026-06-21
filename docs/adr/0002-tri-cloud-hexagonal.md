# ADR-0002: Tri-cloud via Hexagonal — core cloud-agnostic, adapter & host per-cloud

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Lintas-cutting — semua modul, `Platform.<Cloud>`, `Hosts/`, `deploy/`

## Context

Sandbox harus deploy ke **tiga target — Local, Azure, GCP** — dan siap "cloud ke-N" tanpa rombak (tujuan AZ-204 + GCP PCD menuntut dua cloud nyata). Risiko utamanya: SDK cloud (`Azure.*`, `Google.*`) merembes ke domain/application sehingga logika bisnis terkunci ke satu vendor dan tak bisa dites tanpa cloud.

## Decision

- **Pilihan:** Terapkan **Hexagonal (Ports & Adapters)** lintas dimensi cloud. **Domain + Application nol cloud SDK**; tiap cloud punya project **adapter** (`Platform.<Cloud>`) + **host** (`Hosts/<Cloud>`) sendiri. Isolasi SDK terjadi di **batas project reference** — host yang me-reference adapter satu cloud tak menyeret SDK cloud lain ke build.
- **Kenapa:** Dependency Inversion menempatkan abstraksi (port: `IMessagePublisher`, `ISecretProvider`, …) di core dan detail volatile (SDK) di tepi. Ganti cloud = ganti **isi adapter**, struktur & core tak goyang. `→ Canon: Cockburn (Hexagonal/Ports & Adapters), originator; Hombergs (GYHDoCA), port/adapter dalam praktik; Martin (Clean Architecture), Dependency Rule`.
- **Trade-off:** Lebih banyak project (adapter + host per cloud) dan satu lapis indireksi port; untuk app single-cloud ini overkill.
- **Kapan ditinjau ulang:** Bila tri-cloud dibatalkan jadi single-cloud permanen → port abstrak bisa dilonggarkan (tapi tetap berguna untuk testability).

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Hexagonal, core agnostic + adapter/host per-cloud** *(dipilih)* | Cloud ke-N = tambah 1 kolom adapter; core testable tanpa cloud | Indireksi port + lebih banyak project | Cockburn; Hombergs (GYHDoCA) |
| B. SDK langsung di Application (vendor-coupled) | Lebih sedikit kode awal | Vendor lock-in; mustahil tri-cloud tanpa rewrite; sulit dites | Martin (Clean Architecture) |
| C. Abstraksi runtime pihak-ketiga (mis. multicloud lib) | Satu API untuk banyak cloud | Lowest-common-denominator; tak melatih SDK tiap cloud (kontra tujuan cert) | Newman (Building Microservices) |

## Consequences

**Positif**
- Litmus test seam terpenuhi: nambah cloud menyentuh **hanya** adapter + host + IaC — nol perubahan Domain/Application.
- Core bisa dites penuh secara lokal tanpa kredensial cloud.
- Tiap cloud bebas memetakan port ke layanan yang berbeda ([ADR-0018](0018-compute-hosting-mixed-paas.md)) tanpa menyentuh bisnis.

**Trade-off / lebih sulit**
- Setiap concern baru harus didefinisikan dua-tiga kali: port (agnostic) + implementasi per cloud.

**Yang harus dijaga**
- Fitness function #1 & #6 ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)) gagalkan build bila SDK cloud bocor keluar `Platform.<Cloud>`/Hosts.

## Out of scope / deferred

- Pemilihan layanan cloud konkret per port = keputusan deploy-time di `deploy/` ([ADR-0018](0018-compute-hosting-mixed-paas.md)), bukan di sini.
- AWS atau cloud ke-4 belum di-scope; strukturnya sudah siap menampung.

## Amendment — 2026-06-20

> Keputusan Hexagonal di atas tak berubah; blok ini melengkapi **inventory port** yang kini dikenal.

**Named ports otoritatif (di `BuildingBlocks` — implementasi per `Platform.<Cloud>`):**
- `IMessagePublisher`, `ISecretProvider` (asli).
- `ICacheStore` (get/set/remove + TTL) — satu adapter StackExchange.Redis dipakai **dua cloud** (Azure Cache for Redis & GCP Memorystore, RESP-compatible); InMemory untuk Local. Lihat [ADR-0011](0011-master-data-read-api-cache-aside.md).
- `IServiceTokenProvider` — bearer audience-scoped untuk service-to-service; adapter Managed Identity / GCP SA OIDC / Local stub. Lihat [ADR-0021](0021-service-to-service-auth.md).
- `IDelayedTaskQueue` + `ITelemetryStream` — trigger time-driven durable & telemetry fail-open. Lihat [ADR-0025](0025-cross-cutting-platform-ports.md).
- `IPasswordHasher` — KDF lambat (Argon2id) di balik format opaque self-describing. Lihat [ADR-0016](0016-refresh-token-rotation.md).
