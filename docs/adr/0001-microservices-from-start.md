# ADR-0001: Microservices sejak awal (bukan modular monolith dulu)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Seluruh sistem TomSandboxWMS — 3 core (Inbound, Inventory, Outbound) + 4 supporting (MasterData, Auth, Reporting, Notification)

## Context

Default enterprise yang sehat untuk domain seukuran ini adalah **modular monolith dulu** — pisah ke service terdistribusi hanya saat ada pemicu nyata: scaling independen, deploy cadence berbeda, otonomi tim, data store berbeda. TomSandboxWMS adalah **sandbox belajar solo** dengan tiga tujuan: (1) enterprise distributed architecture, (2) AZ-204, (3) GCP Professional Cloud Developer.

Tegangan inti yang harus diakui eksplisit: beban yang biasanya jadi alasan **menunda** microservices (distributed debugging, eventual consistency, ops banyak service) di sini justru **adalah materi yang ingin dilatih**. Keputusan ini karena itu menyimpang dari default anti-overengineering — dan disengaja.

## Decision

- **Pilihan:** Bangun sebagai **microservices sejak awal** — satu service per bounded context, DB-per-service ([ADR-0010](0010-data-ownership-db-per-service.md)), komunikasi via event ([ADR-0005](0005-event-driven-outbox.md)) + gRPC ([ADR-0006](0006-grpc-internal-rest-ui.md)).
- **Kenapa:** Learning objective *adalah* requirement-nya. Distribusi, broker, Outbox/saga, observability lintas-service, dan deployment multi-cloud adalah persis matriks yang diuji AZ-204/PCD dan inti latihan arsitektur enterprise. `→ Canon: Newman (Building Microservices), definisi batas service & otonomi; Ford et al. (Hard Parts), service granularity disintegrators`.
- **Trade-off:** Accidental complexity nyata ditanggung **sengaja** — distributed transaction, partial failure, ops 7 service untuk satu orang. Untuk produk nyata dengan tim + deadline, modular monolith jelas lebih bijak.
- **Kapan ditinjau ulang:** Bila tujuan bergeser dari "latihan arsitektur" ke "deliver produk" dengan constraint waktu/biaya → kolaps ke modular monolith (boundary bersih membuat ini murah; bukan rewrite).

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Microservices from start** *(dipilih)* | Melatih distribusi penuh; selaras cert AZ-204/PCD; boundary tegas sejak awal | Accidental complexity & ops berat untuk solo — *deviasi sadar dari default* | Newman (Building Microservices) |
| B. Modular monolith dulu, extract saat ada pemicu | Default enterprise yang sehat; simpel; refactor murah lewat boundary | Tidak melatih distribusi yang justru jadi tujuan #1 | Newman (Monolith to Microservices); skill default |
| C. Layered monolith tradisional (no module boundary) | Paling cepat jalan | Tak melatih apa pun yang relevan; meluncur ke big-ball-of-mud | Richards & Ford (Fundamentals) |

## Consequences

**Positif**
- Semua pattern terdistribusi (Outbox, broker, DLQ, idempotency, saga) punya tempat alami untuk dilatih.
- Tiap service bisa deploy & scale independen di compute yang sesuai profil ([ADR-0018](0018-compute-hosting-mixed-paas.md)).
- Cocok langsung dengan struktur blueprint (modul = bounded context = unit deploy).

**Trade-off / lebih sulit**
- Tiada strong consistency lintas-context; harus berpikir eventual consistency sejak hari pertama.
- Observability & local orchestration jadi prasyarat, bukan opsional ([ADR-0008](0008-aspire-distributed-local.md)).

**Yang harus dijaga**
- Disiplin boundary ditegakkan otomatis oleh 6 fitness function ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)) — tanpa itu, "microservices" diam-diam jadi distributed monolith.

## Out of scope / deferred

- Jumlah & granularitas service final mengikuti daftar bounded context di overview; pemecahan lebih lanjut (mis. QC sebagai context terpisah) di-defer sampai domain-nya di-scope.
- Strategi konsolidasi balik ke monolith tidak dirinci di sini — cukup dicatat sebagai jalur murah bila pemicunya muncul.
