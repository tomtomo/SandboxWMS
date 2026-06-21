# ADR-0014: Snapshot field master-data kritikal ke aggregate transaksional

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Aggregate transaksional yang merefer MasterData — `GoodsReceipt.expectedLines[]`, `OutboundOrder.orderLines[]`

## Context

Dokumen transaksional (GR, OutboundOrder) merefer Master Data (Product `uom`, `batchTrackingRequired`, dll) lewat ID ([ADR-0011](0011-master-data-read-api-cache-aside.md)). Tapi master data **berubah** seiring waktu. Jika dokumen historis selalu membaca nilai master **terkini**, GR bulan lalu bisa "berubah makna" saat `uom` produk diganti — merusak integritas catatan historis.

## Decision

- **Pilihan:** **Snapshot** field master-data **kritikal** ke aggregate transaksional saat dokumen dibuat (mis. `uom`, `batchTrackingRequired` di `expectedLines[]`/`orderLines[]`). Field **non-kritikal** boleh tetap **reference by ID** (dibaca terkini via read-API). Master data **soft-delete only** (`isActive=false`), tak pernah hard delete.
- **Kenapa:** Snapshot membekukan makna dokumen pada saat transaksi (temporal correctness) — perubahan master tak menulis ulang sejarah. Soft-delete menjaga referential integrity dengan dokumen lama. `→ Canon: Vernon (IDDD), aggregate mereferensi yang lain by ID + jaga invariant lokal; Evans (DDD), entity vs value & lifecycle; Kleppmann (DDIA), immutability & historical data`.
- **Trade-off:** Duplikasi data (nilai master tersalin ke banyak dokumen); "snapshot vs live" jadi keputusan sadar per-field, bukan default seragam.
- **Kapan ditinjau ulang:** Bila ada field yang ternyata harus selalu live (mis. status compliance) padahal di-snapshot → pindahkan ke reference; sebaliknya bila field live menyebabkan drift historis → snapshot.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Snapshot kritikal + reference non-kritikal + soft-delete** *(dipilih)* | Dokumen historis stabil; integritas terjaga; granular | Duplikasi; perlu klasifikasi per-field | Vernon (IDDD); Evans (DDD) |
| B. Selalu reference by ID (baca master terkini) | Tak ada duplikasi; selalu "fresh" | Dokumen historis berubah makna saat master berubah | Kleppmann (DDIA) |
| C. Salin seluruh master ke tiap dokumen | Maksimal stabil | Bloat & sulit di-maintain; over-snapshot | Fowler (PoEAA) |

## Consequences

**Positif**
- GR/OutboundOrder lama tetap akurat meski Product berubah — audit & reporting konsisten ([ADR-0017](0017-eventual-consistency-reporting-notification.md)).
- Mengurangi ketergantungan runtime ke MasterData untuk data yang sudah dibekukan.

**Trade-off / lebih sulit**
- Tim harus secara sadar memutuskan field mana kritikal (snapshot) vs non-kritikal (reference) — bukan refleks.
- Snapshot **melengkapi**, bukan menggantikan, read-API + cache ([ADR-0011](0011-master-data-read-api-cache-aside.md)).

**Yang harus dijaga**
- Hard delete master dilarang (akan break referensi historis); enforce `isActive` flag.

## Out of scope / deferred

- Daftar definitif "field kritikal vs non-kritikal" per aggregate akan dikunci saat implementasi tiap fitur.
- Versioning master data (riwayat perubahan master) belum di-scope; snapshot di dokumen sudah cukup untuk stabilitas historis.

## Amendment — 2026-06-20

> Snapshot + soft-delete di atas tetap. Blok ini menambah panduan interaksi dengan EF global query filter.

- **Soft-delete query filter & targeted bypass**: saat global query filter soft-delete (`isActive`) dipasang, operasi yang perlu melihat baris non-aktif harus pakai bypass yang **ter-target nama-filter**, bukan blanket `IgnoreQueryFilters` (yang mematikan semua filter sekaligus, termasuk warehouse-scoping kelak). Berlaku juga di gRPC read-port ([ADR-0011](0011-master-data-read-api-cache-aside.md)); melindungi soft-delete invariant ADR ini + filter warehouse-scoping ([ADR-0012](0012-deferred-authorization-enforcement.md)) saat co-exist. `→ Canon: MS Learn, EF Core Global Query Filters & IgnoreQueryFilters`.
