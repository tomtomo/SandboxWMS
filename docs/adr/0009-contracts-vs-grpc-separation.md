# ADR-0009: Pisahkan `*.Contracts` (integration event POCO) dari `*.Grpc` (.proto sync)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** `<Module>.Contracts`, `<Module>.Grpc` — published language antar-service

## Context

Sebuah modul mengekspos dua jenis kontrak publik yang berbeda sifat: (a) **integration event** asinkron lewat broker ([ADR-0005](0005-event-driven-outbox.md)); (b) **API sinkron** gRPC ([ADR-0006](0006-grpc-internal-rest-ui.md)). Keduanya "published language" tapi punya dependency, siklus evolusi, dan konsumen yang berbeda. Menggabungkannya menyeret transport dependency ke konsumen yang hanya butuh event POCO.

## Decision

- **Pilihan:** Dua project terpisah. **`<Module>.Contracts`** = integration event sebagai **POCO record, ZERO transport/serialization dependency**, publik & ber-versi. **`<Module>.Grpc`** = `.proto` + stub sync (depend Grpc/Protobuf), terpisah dan **opsional** (hanya bila modul expose sync API).
- **Kenapa:** Konsumen event tak boleh dipaksa menarik stack gRPC; kontrak event yang dependency-free aman jadi versioned package saat split ([ADR-0007](0007-monorepo-with-polyrepo-path.md)). Memisah keduanya = Published Language yang bersih per gaya komunikasi. `→ Canon: Evans (DDD), Published Language & Anti-Corruption Layer; Bellemare (Event-Driven Microservices), desain & evolusi event schema; Ford et al. (Hard Parts), distributed contracts`.
- **Trade-off:** Dua artefak kontrak per modul (kadang hanya `.Contracts` yang ada).
- **Kapan ditinjau ulang:** Bila format kontrak diseragamkan (mis. Protobuf juga untuk event) → pemisahan bisa ditinjau, tapi dependency-isolation tetap argumen kuat.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. `.Contracts` (POCO) terpisah dari `.Grpc`** *(dipilih)* | Event dependency-free; konsumen event tak narik gRPC; split jadi package mudah | Dua project kontrak | Evans (DDD); Bellemare |
| B. Satu project kontrak gabungan (event + proto) | Lebih sedikit project | Transport dep bocor ke konsumen event; evolusi terkunci jadi satu | Ford et al. (Hard Parts) |
| C. Domain type langsung jadi wire-contract | Nol mapping | Internal model bocor; perubahan internal memecah konsumen | Evans (DDD); Bellemare |

## Consequences

**Positif**
- Tipe Domain internal **tak pernah** jadi wire-contract; ACL di consumer menerjemahkan contract asing ke model sendiri ([ADR-0005](0005-event-driven-outbox.md)).
- `*.Contracts` (dependency-free) = kandidat sempurna untuk versioned package saat polyrepo ([ADR-0007](0007-monorepo-with-polyrepo-path.md)).

**Trade-off / lebih sulit**
- Evolusi kontrak harus disiplin: tambah versi baru, versi lama tetap, konsumen opt-in saat siap.

**Yang harus dijaga**
- `<Module>.Contracts ──▶ (nothing)`; `<Module>.Grpc ──▶ (Grpc/Protobuf)` — ditegakkan dependency rule ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)).

## Out of scope / deferred

- Schema registry & contract test otomatis (mis. Pact consumer-driven) disebut di struktur tests; pengaktifannya di-defer.
- Strategi versioning detail (header versi event, namespace per versi) belum dibakukan.
