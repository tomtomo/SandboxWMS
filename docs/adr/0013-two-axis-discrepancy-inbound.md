# ADR-0013: Two-axis discrepancy model di Inbound (lineStatus × quantityVariance)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Inbound — aggregate `GoodsReceipt` (state Pending: `quantityChecks[]`, `discrepancies[]`, `resolutions[]`)

## Context

Saat penerimaan barang, satu SKU bisa bermasalah dalam **dua dimensi yang independen**: kondisi item (operator tag: `Good`/`WrongItem`/`QcHold`) dan selisih kuantitas (sistem hitung: `Normal`/`ShortDelivery`/`OverDelivery`). Contoh nyata: datang 100 carton (`OverDelivery`) tapi 5 rusak (`QcHold`) — dua masalah pada SKU yang sama, butuh dua keputusan resolusi berbeda.

## Decision

- **Pilihan:** Model discrepancy sebagai **dua sumbu independen**, bukan satu enum tunggal. **`lineStatus`** (per scan, di-tag operator) dan **`quantityVariance`** (per SKU, dihitung sistem saat scan selesai). Satu SKU yang kena dua sumbu → **dua entry discrepancy terpisah**, masing-masing dengan resolution sendiri.
- **Kenapa:** Memaksakan satu enum gabungan akan meledak kombinatorik (Good×Normal, WrongItem×Over, QcHold×Short, …) dan menyembunyikan bahwa dua sumbu punya **sumber & resolusi berbeda**. Memodelkan sesuai realitas domain = ubiquitous language yang jujur. `→ Canon: Evans (DDD), ubiquitous language & tactical modeling; Vernon (IDDD), desain aggregate & invariant`.
- **Trade-off:** Logika kompilasi `discrepancies[]` lebih kaya (gabungan dua sumber); UI review harus meng-group per SKU/type.
- **Kapan ditinjau ulang:** Bila muncul sumbu ketiga (mis. mismatch batch/expiry) → tambah sebagai sumbu independen, jangan paksa ke dua yang ada.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Dua sumbu independen (status × variance)** *(dipilih)* | Sesuai realitas; resolusi per-masalah; extensible per sumbu | Kompilasi & UI lebih kaya | Evans (DDD); Vernon (IDDD) |
| B. Satu enum discrepancy gabungan | Sederhana di permukaan | Ledakan kombinatorik; sembunyikan dua sumber berbeda | Evans (DDD) |
| C. Free-text catatan discrepancy | Fleksibel | Tak bisa di-resolve sistematis; tak ada SOP default per type | — |

## Consequences

**Positif**
- Tiap discrepancy punya `action` default sesuai type (ShortDelivery→AcceptPartial, OverDelivery→RejectExcess, WrongItem→ReturnToSupplier, QcHold→SendToQC) → SOP eksplisit.
- Payload `GRConfirmed` (`receivedLines[]`/`rejectedLines[]`) jadi turunan langsung dari resolusi per-sumbu ([ADR-0005](0005-event-driven-outbox.md)).

**Trade-off / lebih sulit**
- Invariant "tiap discrepancy harus punya resolution sebelum Confirm" harus dijaga aggregate `GoodsReceipt`.

**Yang harus dijaga**
- Struktur konseptual (`discrepancies[]`) di-normalize ke tabel terpisah (`gr_discrepancies`) tapi tetap satu aggregate di domain — invariant ditegakkan di domain, bukan DB.

## Out of scope / deferred

- Flow inspeksi QC penuh (release/reject dari Quarantine) belum di-scope — `QcHold` saat ini hanya menempatkan stock di Quarantine (behavior Inventory, di luar set ADR ini).
- Return-to-vendor untuk `rejectedLines` baru tercatat sebagai metadata event; flow detail menyusul.
- Picking discrepancy (Outbound) akan mengikuti pola serupa saat di-scope.
