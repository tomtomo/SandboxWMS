# ADR-0031: Optimistic concurrency token (PostgreSQL `xmin`) pada aggregate root

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-23
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Persistence lintas-context (EF Core + PostgreSQL, [ADR-0010](0010-data-ownership-db-per-service.md)); aggregate root tiap service; aggregate `RefreshToken` (Auth, [ADR-0016](0016-refresh-token-rotation.md)); error transport [ADR-0019](0019-error-handling-result-transport-mapping.md)

## Context

Audit Phase 04.5 menemukan **nol** concurrency token di seluruh `src` (terverifikasi: grep `xmin|rowversion|IsConcurrencyToken|UseXminAsConcurrencyToken` = 0 match). Write-model menulis via `EfUnitOfWork.SaveChangesAsync` tanpa pengaman *lost-update*: dua transaksi konkuren yang sama-sama membaca state sebuah aggregate, lalu menulis, akan saling menimpa secara diam-diam (last-writer-wins).

Guard state **in-aggregate** tidak menutup ini karena guard dievaluasi terhadap snapshot yang dibaca masing-masing transaksi:

- **`Stock`** dimutasi oleh ≥3 trigger berbeda (Putaway / Allocate / Pick) di transaksi terpisah. Guard `Status != Available` di `Stock.Allocate` (`Stock.cs:106-114`) **tidak** melindungi dua transaksi yang sama-sama membaca `Available` lalu meng-alokasi ke wave berbeda → **fork** (lost-update inventory).
- **`RefreshToken` rotation-fork reachable HARI INI** lewat *request-concurrency* (dua refresh request paralel di thread/DbContext berbeda), bukan menunggu multi-replica. Dua refresh paralel sama-sama lolos `IsActive` → dua successor aktif → cascade `ReplacedByTokenId` (single-pointer, `RefreshHandler.cs:68-85`) gagal mencabut salah satu cabang → **orphan branch lolos** → **replay-detection defeat**. Ini lubang **security**, bukan sekadar lost-update — ia mengalahkan justru tujuan reuse-detection [ADR-0016](0016-refresh-token-rotation.md).

Amendment [ADR-0016](0016-refresh-token-rotation.md) sudah **menyebut** concurrent-refresh sebagai edge case "to test", tetapi **mekanisme konkret** (concurrency token + pemetaan exception → error) belum tercatat sebagai keputusan. Multi-replica di Phase 05b (ACA/KEDA) hanya akan **memperparah** permukaan ini; menutupnya sekarang lebih murah daripada setelah replikasi.

## Decision

- **Pilihan:** Adopsi **Optimistic Offline Lock** dengan memetakan kolom sistem PostgreSQL **`xmin`** sebagai concurrency token via `UseXminAsConcurrencyToken()` pada **tiap aggregate root** (di `IEntityTypeConfiguration<T>` masing-masing) **dan** pada `RefreshToken`. EF Core menambahkan `WHERE xmin = @original` pada setiap `UPDATE`/`DELETE`; bila row sudah berubah (rows-affected = 0) EF melempar `DbUpdateConcurrencyException`. Exception itu di-tangkap di satu seam tulis (`EfUnitOfWork` / `TransactionBehavior`) dan dipetakan ke **`Error.Conflict`** (kode error stabil, sudah ada di taksonomi) → REST 409 / gRPC `Aborted` lewat `ErrorTransport` ([ADR-0019](0019-error-handling-result-transport-mapping.md)). Ditambah integration test: **concurrent-refresh** (membuktikan no rotation-fork) + **concurrent-allocate**.
- **Kenapa:** Perubahan state konkuren pada aggregate harus aman tanpa lock pesimistik yang menahan koneksi. `xmin` adalah **system column** PostgreSQL — **zero-schema-cost** (tak ada migration, tak ada kolom baru), satu baris konfigurasi per aggregate. `Conflict` (409) sudah ada di transport sehingga taksonomi error dapat *producer* nyata pertamanya. Menutup rotation-fork = memperbaiki *security invariant* [ADR-0016](0016-refresh-token-rotation.md), bukan sekadar correctness inventory. `→ Canon: Fowler (PoEAA), Optimistic Offline Lock; Vernon (IDDD) ch.10, concurrency & aggregate consistency boundary; Kleppmann (DDIA), write skew & lost update.`
- **Trade-off:** Handler yang menulis kini bisa menerima `Result` ber-`Error.Conflict` → harus ditangani (umumnya: surface 409 ke client; untuk operasi idempotent, retry/re-read). Test yang memutasi aggregate **dua kali berturut** dalam satu skenario harus me-*refresh* entity di antaranya (token berubah tiap commit). Write-amplification kecil pada event yang ter-retry. Tidak menggantikan **pessimistic lock**/`FOR UPDATE SKIP LOCKED` untuk competing-consumer dispatcher multi-instance — itu concern broker nyata yang tetap di-defer ke Phase 05/06 ([ADR-0029](0029-local-in-process-messaging-delivery.md)).
- **Kapan ditinjau ulang:** Bila beralih ke broker lintas-proses + multi-replica (Phase 05b/06b) dan dispatcher butuh row-level lock — concurrency-token tetap berlaku, ditambah lock pesimistik di sisi dispatch. Bila pindah ke store non-PostgreSQL (mis. Cosmos) → ganti mekanisme token (ETag) sambil mempertahankan pemetaan `DbUpdateConcurrencyException`→`Conflict`.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. `xmin` optimistic token per aggregate root + map exception→`Conflict`** *(dipilih)* | Zero-schema-cost (system column); satu baris/aggregate; tutup rotation-fork & lost-update; `Conflict` taxonomy dipakai | Handler harus tangani conflict-result; test mutasi-berturut harus refresh; bukan pengganti lock dispatcher | Fowler (PoEAA); Vernon (IDDD) |
| B. `rowversion`/`byte[]` token kolom eksplisit | Portable lintas-DB | Butuh migration + kolom + maintenance; redundan dengan `xmin` di PostgreSQL | Fowler (PoEAA) |
| C. Pessimistic lock (`SELECT … FOR UPDATE`) di write | Cegah konflik di sumber | Menahan koneksi; throughput turun; over-engineered untuk sandbox single-host; concern sebenarnya broker-side | Nygard (Release It!) |
| D. Biarkan (status quo) | Nol kerja | `RefreshToken` rotation-fork = security hole reachable hari ini; lost-update inventory diam | — |

## Consequences

**Positif**
- Transisi state konkuren aman; rotation-chain `RefreshToken` **single-writer** → reuse-detection [ADR-0016](0016-refresh-token-rotation.md) tak bisa di-bypass via refresh paralel.
- `Error.Conflict` (409) mendapat *producer* nyata → taksonomi error [ADR-0019](0019-error-handling-result-transport-mapping.md) lengkap end-to-end.
- Lubang ditutup **sebelum** multi-replica Phase 05b memperparahnya — murah sekarang, mahal nanti.

**Trade-off / lebih sulit**
- Handler write harus memetakan/menyurfakan conflict; test idempotensi yang memutasi aggregate berkali harus me-refresh entity di antara commit.
- `DbUpdateConcurrencyException` harus ditangkap di seam tunggal — jangan bocor sebagai 500.

**Yang harus dijaga**
- Token dipasang di **tiap aggregate root** (bukan child entity) — child ikut transaksi root (consistency boundary, [ADR-0026](0026-tactical-ddd-conventions.md)). (Opsional) fitness function mengunci invariant "tiap aggregate root punya concurrency token" agar tak regress.
- Pemetaan `DbUpdateConcurrencyException`→`Error.Conflict` hidup di satu seam tulis; tidak diduplikasi per handler.

## Out of scope / deferred

- `FOR UPDATE SKIP LOCKED` / advisory-lock untuk Outbox dispatcher multi-instance — di-defer Phase 05/06 ([ADR-0029](0029-local-in-process-messaging-delivery.md)); concurrency-token tidak menggantikannya.
- Retry otomatis on-conflict (mis. Polly) — saat ini surface `Conflict` ke caller; kalibrasi resilience di [ADR-0020](0020-resilience-pipeline-defaults.md) / Phase 07c.
- Token untuk projection/read store ([ADR-0017](0017-eventual-consistency-reporting-notification.md)) — read-side rebuild-able, tak butuh optimistic lock.
