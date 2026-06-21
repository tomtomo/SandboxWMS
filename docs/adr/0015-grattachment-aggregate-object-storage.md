# ADR-0015: `GRAttachment` sebagai aggregate terpisah + byte di object storage

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Inbound — `GRAttachment` (tabel `inbound.gr_attachments`), object storage; tertaut ke `GoodsReceipt` via `goodsReceiptId`

## Context

Satu `GoodsReceipt` butuh dokumen pendukung (surat jalan/ASN, PO, foto proof-of-delivery) yang di-upload operator **bertahap** dan bisa **banyak**. Dua tegangan: (1) menjadikannya child `GoodsReceipt` memaksa full-load GR setiap upload & membengkakkan aggregate; (2) menyimpan byte di database membebani DB & mahal di-query.

## Decision

- **Pilihan:** `GRAttachment` = **aggregate root tersendiri** (bukan child `GoodsReceipt`), punya repository + tabel sendiri, tertaut via **logical FK** `goodsReceiptId` (tanpa navigation property), dan **tidak** memancarkan event lintas-modul. **Byte content di object storage**; row hanya menyimpan **metadata + `blobPath`** (pola `{grId}/{attachmentId}/{fileName}`). Invariant di factory `GRAttachment.Create` (whitelist contentType, `sizeBytes` ≤ 50 MB, dll); immutable kecuali soft-delete.
- **Kenapa:** Aggregate kecil + referensi by ID adalah aturan desain aggregate kanonik — upload bertahap tanpa menyentuh konsistensi GR. Object storage adalah tempat tepat untuk blob besar; DB menyimpan metadata yang query-able. `→ Canon: Vernon (IDDD), aggregate kecil & reference by ID; Evans (DDD), aggregate boundary; Fowler (PoEAA), pemisahan metadata vs blob`.
- **Trade-off:** Konsistensi GR↔attachment jadi **tanggung jawab aplikasi** (tak ada FK DB / transaksi lintas-aggregate); butuh abstraksi object storage + cleanup byte saat soft-delete.
- **Kapan ditinjau ulang:** Bila attachment perlu memicu alur lintas-modul (mis. OCR/validasi dokumen) → baru tambahkan event; saat ini sengaja silent.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Aggregate terpisah + byte di object storage** *(dipilih)* | Aggregate GR ramping; upload bertahap; blob murah & skalabel | Konsistensi lintas-aggregate di app; perlu port storage | Vernon (IDDD); Evans (DDD) |
| B. Child entity di `GoodsReceipt`, byte di object storage | Satu aggregate, FK kuat | Full-load GR tiap upload; aggregate membengkak | Vernon (IDDD) |
| C. Simpan byte di kolom DB (BLOB) | Transaksi tunggal, simpel | DB bloat, backup mahal, query berat | Kleppmann (DDIA) |

## Consequences

**Positif**
- `GoodsReceipt` tetap fokus pada siklus penerimaan; attachment di-query independen (index per `goodsReceiptId`).
- Object storage di-abstraksi via port → implementasi per-cloud (Blob/GCS/local) ([ADR-0002](0002-tri-cloud-hexagonal.md)).

**Trade-off / lebih sulit**
- "Orphan blob/row" (byte ada tanpa metadata atau sebaliknya saat gagal parsial) harus dicegah lewat urutan tulis + cleanup yang disiplin.

**Yang harus dijaga**
- Logical FK tanpa navigation property — `GRAttachment` tak boleh menyeret load `GoodsReceipt` (jaga pemisahan aggregate).

## Out of scope / deferred

- Antivirus scan / content validation byte, thumbnailing, signed-URL download → di-defer.
- Lifecycle/retention byte (auto-archive) = kebijakan storage deploy-time.
