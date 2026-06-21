# ADR-0026: Tactical DDD conventions — identity, domain-event emission, value-object ownership

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** `*.Domain` semua modul; seedwork `BuildingBlocks.Domain` (StronglyTypedId, IDomainEvent, ValueObject)

## Context

Seedwork memberi primitive (`StronglyTypedId`, `IDomainEvent`, `ValueObject`) tapi **tanpa konvensi pemakaian**, sehingga tiap aggregate berisiko drift: kapan pakai surrogate vs business key, kapan & dari mana domain event di-raise, di mana value object "milik" sebuah context. Konvensi yang tak tertulis akan luntur ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md) berlaku untuk struktur; ini melengkapi untuk taktis).

## Decision

- **Pilihan:** Satu dokumen konvensi taktis (~1 halaman):
  1. **Identity-encoding**: default `StronglyTypedId` surrogate; business key hanya di 3 kondisi (natural-key stabil, eksternal, wajib unik secara domain) + penanda di kode.
  2. **Domain-event emission policy**: event di-raise **dari dalam aggregate**, **hanya** pada fakta bisnis yang sukses; **tak ada** event pada guard yang gagal. **Tidak ada** jalur tulis domain-event langsung ke Outbox — satu-satunya jalur: domain event → integration event → Outbox ([ADR-0005](0005-event-driven-outbox.md)); write out-of-band (audit) pakai `IAuditLogStore`, bukan Outbox ([ADR-0022](0022-operational-audit-log.md)).
  3. **Value-object ownership/placement**: VO bisnis dimiliki context asalnya (di `*.Contracts` bila menyeberang); **dilarang** menaruh VO bisnis di SharedKernel/`BuildingBlocks.Domain`.
  4. **Behavioral fitness category (baru)**: test runtime yang memverifikasi method aggregate me-raise event-nya. Ini **kategori test perilaku yang hidup di test suite**, **bukan** FF statik NetArchTest #7–#11 di registry [ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md) (NetArchTest tak bisa introspeksi body method).
- **Kenapa:** Emission policy inilah yang membuat pipeline EDA ([ADR-0005](0005-event-driven-outbox.md)) tepercaya dan projection ([ADR-0017](0017-eventual-consistency-reporting-notification.md)) rebuild-able; VO-ownership menjaga boundary context ([ADR-0009](0009-contracts-vs-grpc-separation.md), [ADR-0010](0010-data-ownership-db-per-service.md)). `→ Canon: Evans (DDD), tactical patterns (Entity/VO/Aggregate/Domain Event); Vernon (IDDD), aturan aggregate & domain event; Khononov (LDDD), pemilihan pattern per subdomain`.
- **Trade-off:** Konvensi harus dirawat & disosialisasikan (sendiri pun perlu disiplin); behavioral test menambah suite.
- **Kapan ditinjau ulang:** Bila sebuah subdomain butuh pola lain (mis. event sourcing) → konvensi diperluas, bukan dipaksakan seragam.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Conventions doc + behavioral FF category** *(dipilih)* | Cegah drift taktis; bikin EDA tepercaya; murah | Perlu dirawat; test perilaku tambahan | Evans (DDD); Vernon (IDDD) |
| B. Andalkan seedwork + review manual | Nol dokumen | Drift; emission policy implicit; projection rapuh | Khononov (LDDD) |
| C. Hard-enforce semua via analyzer | Ketat | Mahal dibuat; sebagian aturan tak bisa statik | Richards & Ford (Fundamentals) |

## Consequences

**Positif**
- Emission policy konsisten → Outbox & projection dapat diandalkan; identity & VO placement seragam lintas-modul.
- Behavioral FF menambah dimensi governance yang sebelumnya tak ada (statik → perilaku).

**Trade-off / lebih sulit**
- Behavioral test perlu setup per aggregate (lebih mahal dari NetArchTest statik).

**Yang harus dijaga**
- VO bisnis tak pernah masuk `BuildingBlocks.Domain`; event hanya dari dalam aggregate pada fakta sukses.

## Out of scope / deferred

- Konten domain-spesifik (contoh Product/Stock/VO Qty) → ditolak; dokumen ini rules-only.
- Scaffolding `[Obsolete] IOutboxWriter` (side-channel saga yang deferred) → tidak diadopsi (spekulatif).
