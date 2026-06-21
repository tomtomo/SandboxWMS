# ADR-0004: CQRS + Vertical Slice di dalam tiap modul

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Layer Application tiap modul (`<App>.<Module>.Application/Features/<UseCase>`)

## Context

Layer Application bisa diorganisir per-folder-teknis (Controllers/Services/Repositories) atau per use-case. Layout teknis menyebarkan satu fitur ke banyak folder (shotgun surgery) dan memaksa read memutar lewat aggregate yang sebenarnya hanya butuh proyeksi datar. Domain WMS kaya read (dashboard, summary, list task) yang beda bentuk dari write-model-nya.

## Decision

- **Pilihan:** Pisahkan **command** dan **query** (CQRS), dan organisir Application **per vertical slice / use-case** (command-query + handler + validator + endpoint, self-contained), bukan per-folder-teknis. Sisi command lewat aggregate; sisi query baca langsung ke read-DTO, **bypass aggregate/repository**.
- **Kenapa:** Write butuh invariant lewat aggregate; read butuh bentuk query-optimized tanpa beban domain model. Vertical slice menjaga perubahan satu fitur terlokalisasi (high cohesion). `→ Canon: Vernon (IDDD), implementasi domain selaras CQRS; Fowler (PoEAA), Domain Model vs read path`.
- **Trade-off:** Dua model (write & read) untuk data yang sama; boilerplate per slice.
- **Kapan ditinjau ulang:** Untuk subdomain CRUD murni tanpa invariant, CQRS penuh overkill → cukup layering tipis + EF (lihat right-sizing blueprint).

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. CQRS + Vertical Slice** *(dipilih)* | Read cepat & bebas; fitur terlokalisasi; selaras event-sourcing read-side ([ADR-0017](0017-eventual-consistency-reporting-notification.md)) | Dua model; boilerplate slice | Vernon (IDDD); Fowler (PoEAA) |
| B. Layered teknis + satu model (CRUD via aggregate) | Satu model; familiar | Read berat lewat aggregate; shotgun surgery antar-folder | Fowler (PoEAA) |
| C. Full Event Sourcing untuk write-side | Audit/temporal/replay built-in | Kompleksitas tinggi; tak diminta requirement | Vernon (IDDD); Kleppmann (DDIA) |

## Consequences

**Positif**
- Read path (mis. Reporting projection, list PutawayTask) bisa di-tune tanpa menyentuh write model.
- Slice self-contained mempermudah penempatan permission marker `TODO-AUTH` ([ADR-0012](0012-deferred-authorization-enforcement.md)) dan validasi per use-case.

**Trade-off / lebih sulit**
- Developer harus sadar kapan write (lewat aggregate) vs read (lewat DTO) — disiplin, bukan default otomatis.

**Yang harus dijaga**
- Query handler tidak boleh memanggil repository write/aggregate (menjaga pemisahan); command handler tidak boleh return entity domain mentah ke API.

## Out of scope / deferred

- Read store terpisah secara fisik (read replica / database read khusus) belum di-scope — read & write satu DB per service dulu ([ADR-0010](0010-data-ownership-db-per-service.md)), kecuali Reporting yang memang projection ([ADR-0017](0017-eventual-consistency-reporting-notification.md)).

## Amendment — 2026-06-20

> Melengkapi urutan MediatR pipeline behavior yang sebelumnya tak dispesifikasi.

- **Pipeline ordering**: `Logging → Authorization → Validation → Transaction → Handler` — slot Authorization adalah placeholder (marker `TODO-AUTH`) sampai Authorization Wire-Up ([ADR-0012](0012-deferred-authorization-enforcement.md)); authz & validation **fail-fast sebelum** transaksi dibuka. **`TransactionBehavior` hanya di sisi command** (query bypass aggregate/repo & skip transaksi, konsisten dgn split command/query di Decision atas — mis. scope via marker `ICommand`); `ValidationBehavior` & `TransactionBehavior` mengikuti [ADR-0019](0019-error-handling-result-transport-mapping.md).
