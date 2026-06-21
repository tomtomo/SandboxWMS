# ADR-0012: Deferred authorization enforcement (authN aktif, authZ ditunda)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Auth context + setiap command/handler/endpoint sensitif di core modul

## Context

Authorization granular (permission per action: `Inbound.PostGR`, `Outbound.DispatchWave`, …) menambah friksi saat menulis fitur: tiap skenario test butuh setup user/role/permission, dan granularity bisa berubah sebelum fitur stabil (premature commitment). Tapi **identitas** tetap dibutuhkan sejak awal untuk audit (`createdBy`/`modifiedBy`).

## Decision

- **Pilihan:** **Tunda penegakan authorization** selama belum diminta eksplisit. **Authentication (login + JWT) tetap aktif** → identitas mengalir ke `IAuditable`. Permission codes di tabel `Permission` berfungsi sebagai **planning catalog**, bukan yang aktif di code. Pasang marker `// TODO-AUTH: <Module.Action>` di titik wiring nanti. Aktivasi lewat milestone khusus **"Authorization Wire-Up"** (grep TODO-AUTH → pasang `[Authorize(Permission=...)]` → jalankan authz test suite) sebelum P1 production-ready.
- **Kenapa:** Memisahkan "model permission sudah dirancang" dari "enforcement sudah dipasang" mengurangi distraksi & mempermudah testing tanpa mengorbankan auditability (authN tetap ada). Trade-off ini **dibuat sadar & reversibel** lewat marker yang grep-able. `→ Canon: Newman (Building Microservices), security in microservices; OWASP ASVS, access control sebagai kontrol eksplisit`.
- **Trade-off:** Selama periode deferred, **tidak ada enforcement** — aman hanya karena ini sandbox non-produksi; rilis produksi **wajib** lewati milestone Wire-Up dulu.
- **Kapan ditinjau ulang:** Trigger eksplisit = milestone "Authorization Wire-Up" sebelum P1; atau lebih awal bila ada fitur yang menuntut enforcement untuk dimodelkan benar.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. AuthN aktif, authZ deferred + planning catalog + TODO-AUTH** *(dipilih)* | Fokus business logic; test ringan; audit tetap jalan; aktivasi terlacak | Tak ada enforcement sementara (sandbox-only) | Newman (Building Microservices); OWASP ASVS |
| B. Enforce authZ penuh sejak awal | Aman sejak hari-1; granularity teruji dini | Friksi besar tiap fitur/test; premature granularity | OWASP ASVS |
| C. Tanpa authN & authZ sama sekali | Paling cepat | Tak ada identitas → `IAuditable` pincang; hutang besar | — |

## Consequences

**Positif**
- Penulisan fitur core tak terbebani setup role/permission; `createdBy`/`modifiedBy` tetap terisi dari JWT.
- Saat aktivasi, lokasi wiring deterministik (grep `TODO-AUTH`) → milestone terukur.

**Trade-off / lebih sulit**
- Risiko "lupa aktivasi": dimitigasi dengan menjadikan Wire-Up sebagai gate eksplisit menuju P1.
- Warehouse scoping (user hanya boleh `assignedWarehouseIds`) ikut deferred — dicatat untuk Wire-Up.

**Yang harus dijaga**
- Setiap command/handler sensitif baru **wajib** menanam marker `// TODO-AUTH: <Module.Action>` saat ditulis — agar katalog enforcement tetap lengkap.

## Out of scope / deferred

- Pemasangan `[Authorize(Permission=...)]`, warehouse-scope enforcement, & authz test suite → milestone "Authorization Wire-Up".
- Default: 1 user admin tersedia selama periode deferred.

## Amendment — 2026-06-20

> authZ enforcement tetap deferred. Blok ini menambah dua invariant IdP-independent + satu keputusan **konseptual** warehouse-scoping (mekanismenya tetap deferred ke Wire-Up).

- **`IsActive` filter di SEMUA jalur mint token/claim** (Login, Refresh, sumber claim gRPC): permission dari role/permission `IsActive=false` **tak boleh** bocor ke JWT self-contained. Invariant keamanan yang menggigit jalur credential+refresh yang sudah kita bangun ([ADR-0016](0016-refresh-token-rotation.md)).
- **Warehouse-scoping = operational filter, BUKAN security boundary** (keputusan konseptual; mekanisme tetap deferred): keamanan didelegasikan ke layer RBAC (complete mediation); query filter hanya relevance/convenience. Restriksi baca per-warehouse di masa depan = **permission code RBAC baru**, bukan `HasQueryFilter` hack. Pelihara tabel klasifikasi per-aggregate `scoped|global` (di luar domain model). `→ Canon: Saltzer & Schroeder, complete mediation & least privilege`.
- **Offline token-validation principle**: tiap host validasi signature JWT lokal (public key/JWKS), **tak ada** RPC `ValidateToken` per-request ke Auth (hot-path bersih). Algoritma signing = RS256 ([ADR-0016](0016-refresh-token-rotation.md)).
