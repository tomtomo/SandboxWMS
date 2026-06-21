# ADR-0010: Data ownership — DB-per-service, tak ada shared store

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Setiap modul (`<Module>.Infrastructure` DbContext, schema `<module>`); deploy `deploy/*`

## Context

Coupling terburuk di sistem terdistribusi adalah **shared database** — dua service menulis tabel yang sama → boundary runtuh, schema tak bisa berevolusi independen, deploy saling blok. Microservices ([ADR-0001](0001-microservices-from-start.md)) hanya bermakna bila tiap service **memiliki datanya sendiri**.

## Decision

- **Pilihan:** **DB-per-service**. Tiap modul memiliki datanya sendiri; DbContext modul **hanya** menyentuh schema `<module>`-nya. **Tak ada modul membaca store modul lain** — data lintas-context mengalir **hanya** via event ([ADR-0005](0005-event-driven-outbox.md)) atau read-API ber-kontrak ([ADR-0011](0011-master-data-read-api-cache-aside.md)). Apakah schema-schema itu satu DB fisik atau DB fisik terpisah = keputusan **deploy-time**.
- **Kenapa:** Data ownership tegas adalah prasyarat otonomi service & evolusi schema independen. Boundary dijaga oleh dependency rule + kontrak, bukan kebetulan shared schema. `→ Canon: Ford et al. (Hard Parts), data ownership & decomposition; Newman (Building Microservices), database-per-service; Kleppmann (DDIA), trade-off data terdistribusi`.
- **Trade-off:** Tak ada JOIN lintas-context & tak ada transaksi ACID lintas-service → butuh komposisi via event/read-API + eventual consistency.
- **Kapan ditinjau ulang:** Bila satu service ternyata harus selalu konsisten-kuat dengan yang lain (tanda batas context salah) → pertimbangkan menggabungkan keduanya, bukan share DB.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. DB-per-service (schema terisolasi)** *(dipilih)* | Otonomi & evolusi schema independen; boundary tegas | No cross-context JOIN/ACID; perlu read-API + event | Ford et al. (Hard Parts); Newman |
| B. Shared database, schema per-modul tapi cross-read boleh | JOIN mudah; satu transaksi | Coupling tersembunyi; deploy saling blok; anti-microservice | Newman (Building Microservices) |
| C. Satu schema bersama semua modul | Paling sederhana | Big-ball-of-mud data; mustahil dipisah | Ford et al. (Hard Parts) |

## Consequences

**Positif**
- Tiap service bisa migrasi schema & deploy sendiri; siap split ke polyrepo/DB fisik terpisah ([ADR-0007](0007-monorepo-with-polyrepo-path.md)).
- Memaksa kontrak antar-context jadi eksplisit (event + read-API), bukan implicit lewat tabel.

**Trade-off / lebih sulit**
- Kebutuhan baca data milik service lain harus lewat read-API + cache ([ADR-0011](0011-master-data-read-api-cache-aside.md)); data referensi kritikal di-snapshot ([ADR-0014](0014-snapshot-vs-reference-master-data.md)).
- Konsistensi lintas-context jadi eventual ([ADR-0005](0005-event-driven-outbox.md)).

**Yang harus dijaga**
- Fitness function #3 ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)): tak ada modul me-reference Infrastructure modul lain — penjaga struktural dari aturan "no cross-store read".

## Out of scope / deferred

- One-physical-DB vs DB-fisik-terpisah per environment = deploy-time (`deploy/`), bukan keputusan struktur.
- Distributed transaction / saga untuk konsistensi lintas-service belum di-scope ([ADR-0005](0005-event-driven-outbox.md)).

## Amendment — 2026-06-20

> DB-per-service di atas tetap. Blok ini menambah topologi kepemilikan tabel infrastruktur & operasionalnya.

- **Infra-table ownership**: outbox / inbox / `audit_log` dimiliki & dimigrasi oleh **DbContext tiap modul**, dipetakan via shared `modelBuilder` extension ke schema `infrastructure` di dalam DB per-service (`saga-state` menyusul **hanya bila/ketika** saga deferred [ADR-0005](0005-event-driven-outbox.md) diwujudkan — **tak diprovisioning sekarang**). Catatan: `audit_log` ada di schema sama tapi **write-path-nya out-of-band** via `IAuditLogStore` di luar tx bisnis ([ADR-0022](0022-operational-audit-log.md)) — beda dari outbox/inbox yang commit dalam tx bisnis. **`InfrastructureDbContext` standalone DILARANG** (cegah kontaminasi PK lintas-service) — ditegakkan FF #10 ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)).
- **MigrationRunner**: satu console env-neutral yang meng-apply EF migration ke setiap service DB (connection string di-inject, nol cloud SDK) — menutup lubang operasional "N migration assembly diterapkan bagaimana".
- **`IDeadLetterStore`**: store poison-message forensik (Local: tabel Postgres `dead_letter`) dikonsumsi semua adapter messaging; melengkapi Outbox/Inbox messaging & retry/DLQ ([ADR-0005](0005-event-driven-outbox.md); contoh sisi-consumer [ADR-0017](0017-eventual-consistency-reporting-notification.md)).
- **gRPC reader-delegation**: service gRPC (`*.Api`) wajib lewat read-port modul, **tak query `DbContext` mentah** — FF #8 ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)).
