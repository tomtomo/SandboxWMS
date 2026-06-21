# ADR-0017: Eventual consistency untuk Reporting & Notification (pure consumer)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Supporting context Reporting (read-model/projection) & Notification (delivery), consumer domain event core

## Context

Reporting (StockOnHandView, ReceivingSummary, …) dan Notification menerjemahkan peristiwa core jadi, masing-masing, **projection query-optimized** dan **pesan ke user**. Keduanya **read-only ke core** (tak emit balik) dan **tidak punya domain invariant** sendiri (cermin event). Memaksa mereka konsisten-kuat dengan core akan menambah coupling sinkron yang melawan otonomi service.

## Decision

- **Pilihan:** Reporting & Notification = **pure event consumer**, di-update **eventual consistency** lewat message bus + Outbox ([ADR-0005](0005-event-driven-outbox.md)). Reporting membangun **projection** (denormalized, rebuild-able dari event). Notification meng-enqueue `NotificationDelivery` lalu **worker async** mengirim ke channel (in-app/email/push) dengan **idempotency** + **retry→DLQ**. Keduanya tak pernah emit event balik ke core.
- **Kenapa:** Read-side & notifikasi mentolerir lag sub-second; eventual consistency menukar kesegaran instan dengan decoupling & skalabilitas read. Karena projection di-derive dari event, ia **rebuild-able** (replay). `→ Canon: Kleppmann (DDIA), eventual consistency & derived data; Hohpe & Woolf (EIP), Dead Letter Channel & Guaranteed Delivery; Bellemare (Event-Driven Microservices), event-sourced read models`.
- **Trade-off:** Data report/notifikasi bisa tertinggal dari core (lag naik saat beban tinggi/retry); butuh retain event cukup lama untuk rebuild.
- **Kapan ditinjau ulang:** Bila ada report yang menuntut akurasi real-time strict (tak ada di scope) → pertimbangkan query langsung ke core read-API alih-alih projection.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Eventual via event + Outbox; projection rebuild-able; notif async + DLQ** *(dipilih)* | Decoupled; read di-tune bebas; rebuild-able; resilient delivery | Lag; perlu retain event & idempotency | Kleppmann (DDIA); Hohpe & Woolf (EIP) |
| B. Sinkron query ke core saat report diminta | Selalu fresh | Coupling sinkron; beban ke core write-path; rapuh | Newman (Building Microservices) |
| C. Core menulis langsung ke tabel report (dual-write) | Konsisten seketika | Dual-write inconsistency; coupling store; melawan [ADR-0010](0010-data-ownership-db-per-service.md) | Hohpe & Woolf (EIP) |

## Consequences

**Positif**
- Reporting/Notification menambah konsumen baru tanpa menyentuh producer ([ADR-0005](0005-event-driven-outbox.md)); profil "pure consumer" memetakan ke compute serverless ([ADR-0018](0018-compute-hosting-mixed-paas.md)).
- Projection bisa di-rebuild saat schema berubah/ada bug (selama event di-retain).

**Trade-off / lebih sulit**
- Worker Notification harus idempotent (cek `Sent` sebelum kirim ulang) & punya kebijakan retry/DLQ; kegagalan channel provider di-isolasi, tak boleh propagate ke core.
- Event payload yang dipakai pelaporan harus stabil & cukup informatif ([ADR-0009](0009-contracts-vs-grpc-separation.md)).

**Yang harus dijaga**
- Read-only ke core: Reporting/Notification **tak pernah** emit event balik atau menulis store core.

## Out of scope / deferred

- Migrasi storage projection ke NoSQL/document store (bila volume menuntut) — P1 cukup PostgreSQL denormalized/materialized view.
- Event store dedicated untuk retain jangka-panjang (vs Outbox retention) di-defer.
- Read-side untuk notifikasi (mark-as-read) hanya untuk channel InApp; Email/Push tak di-track.

## Amendment — 2026-06-20

> Eventual consistency + projection rebuild-able di atas tetap. Blok ini memancang **siapa yang commit projection-write** (sebelumnya tak dispesifikasi).

- **Projection-write atomicity**: event-handler Reporting melakukan find-or-create-by-PK lalu mutasi via store, **tapi TIDAK memanggil `SaveChanges` sendiri** — **consumer/message-handler sisi Inbox** (bukan OutboxDispatcher produsen) meng-commit projection-write **+** mark idempotency Inbox dalam **satu transaksi** ([ADR-0005](0005-event-driven-outbox.md)). Mencegah double-count / lost projection saat partial failure (celah yang justru ingin ditutup Inbox).
- **Per-type projection store**: read-side memakai port `I*Store` type-safe per projeksi + adapter EF, **bukan** satu generic store. Generic `IProjectionStore<T>` + adapter Cosmos/Firestore **ditolak** (NoSQL projection tetap deferred; latih objektif cert lewat spike, bukan seam permanen unwired).
