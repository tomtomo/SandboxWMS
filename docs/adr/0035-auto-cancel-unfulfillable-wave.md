# ADR-0035: Auto-cancel unfulfillable wave + return orders to backlog

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-25
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Outbound `Wave` (state baru `Cancelled`) + `OutboundOrder` (InProgress→New release). Di-trigger event-driven saat `StockAllocatedV1` ([ADR-0034](0034-allocation-failure-signaling.md)) tiba dengan `allocations` KOSONG (wave nol-terpenuhi). Mutasi intra-Outbound (Wave + Order dua-duanya aggregate Outbound) → kompensasi LOKAL, bukan saga lintas-context. Lewat Inbox/Outbox ([ADR-0005](0005-event-driven-outbox.md)), optimistic token `xmin` ([ADR-0031](0031-optimistic-concurrency-token-xmin.md)).

## Context

[ADR-0034](0034-allocation-failure-signaling.md) menutup *silent-drop* — line yang tak teralokasi penuh kini ber-sinyal (`OrderLine.Short` + notifikasi). Tapi ia **tidak** menyentuh siklus hidup wave/order. Akibatnya muncul gap **hang** untuk kasus stock NOL:

- WaveReleased → Inventory alokasi nol → `StockAllocatedV1` ber-`allocations` kosong → Outbound buat **0 PickingTask** → `Wave.AttachPickingTasks([])` (tetap `Active`).
- `Wave.MarkReady` menolak bila `_pickingTaskIds.Count == 0` (`WaveErrors.NotAllPicked`) DAN hanya dipicu saat `CompletePicking` — nol task → tak pernah dipanggil. `Dispatch` butuh `Ready`; `OutboundOrder.Close` cuma lewat DispatchWave.
- → **Wave `Active` selamanya, Order `InProgress` selamanya** (overview §C; lihat `Wave.cs:MarkReady`, `CompletePickingHandler`).

Resolusi manual (SPV cancel wave) **tak scale**: pada ~1000 order/hari dengan stock-out sebagian, SPV tak mungkin men-triage tiap wave gantung. Dibutuhkan resolusi **otomatis**. Tapi auto-resolusi punya jebakan: *"stock tidak ada SEKARANG" ≠ "tidak ada SELAMANYA"* — GR/putaway masuk sepanjang hari. Membatalkan **order** (demand customer) secara otomatis-langsung = menolak order yang mungkin bisa dipenuhi beberapa menit lagi (footgun bisnis). Yang aman diotomatiskan adalah membatalkan **wave** (sekadar batch/attempt), bukan order.

## Decision

- **Pilihan:** Saat sebuah wave selesai dialokasi dengan **nol** PickingTask (fully-unfulfillable), Outbound **otomatis**: (1) transisi `Wave: Active → Cancelled` (state terminal BARU); (2) lepas tiap order-nya `InProgress → New` (`ReleaseFromWave`: clear `WaveId`) — **kembali ke backlog**, BUKAN dibatalkan mati. Trigger event-driven di `StockAllocatedConsumer`: `StockAllocatedV1` dipancarkan **tepat sekali per wave** membawa SELURUH hasil alokasi → `allocations` kosong = penanda deterministik "wave nol-terpenuhi". Wave **parsial** (≥1 task) TIDAK di-cancel — lanjut normal, kirim yang ada, line short tetap ter-flag (ADR-0034). Re-wave idempoten: `PlaceInWave` me-reset `OrderLine.AllocationStatus → Pending` (tiap attempt = state alokasi bersih).
- **Kenapa:** Order = demand yang sah; *finalisasi prematur* (auto-`Cancelled`) menolak customer hanya karena rak kosong sesaat. Mengembalikan ke `New` bersifat **non-destruktif** — order selamat, masuk antrian, bisa di-wave ulang saat stock tiba. Wave = pengelompokan ephemeral; membubarkan wave nol-alokasi aman (tak ada Stock ter-reservasi → nol yang perlu di-release). Kompensasi ditaruh di Outbound karena Wave & Order dua-duanya milik Outbound (intra-context) — **tak butuh saga lintas-service**. Event-driven (bukan scheduled sweep) → resolusi **seketika & scale-free** (tak ada backlog job yang membengkak pada 1000+/hari). `→ Canon: Richardson (Microservices Patterns), compensating action; Nygard (Release It!), autonomous recovery tanpa intervensi manual; Bellemare, event-driven reaction; Evans (DDD), aggregate sebagai entry-point transisi.`
- **Trade-off:** +1 state `WaveStatus.Cancelled` + 2 method domain (`Wave.Cancel`, `OutboundOrder.ReleaseFromWave`) + reset-on-`PlaceInWave`; `StockAllocatedConsumer` kini punya dua tanggung jawab (buat task **atau** bubarkan wave). Order yang stock-nya **tak pernah** datang akan **memantul** `New → wave → cancel → New` tiap di-wave ulang (tak ada infinite-loop mesin: re-wave = aksi terpisah, bukan otomatis) — long-tail "order yang memang takkan pernah bisa" butuh expiry/timeout terpisah (di-defer). Tak mencegah oversell (itu reservation + `xmin`, ADR-0031); ia hanya merapikan siklus hidup.
- **Kapan ditinjau ulang:** Saat **backorder** (waiting-state + retry-on-PutawayCompleted) atau **order-expiry** (sweep timeout → `Cancelled`) di-scope — keduanya menutup long-tail mantul; revisit apakah `New` perlu dipecah jadi `New` vs `Backordered`.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Auto-cancel wave + order→New (backlog)** *(dipilih)* | Otomatis, scale-free, seketika; non-destruktif (order selamat, re-waveable); intra-context (no saga); nol scheduler | `StockAllocatedConsumer` dua-peran; long-tail mantul belum tertutup | Richardson; Nygard; Bellemare |
| B. Manual SPV cancel wave | Kontrol penuh manusia | Tak scale (1000/hari); wave gantung menumpuk | — |
| C. Auto-cancel **order** → terminal `Cancelled` | Siklus benar-benar tutup | **Footgun**: auto-refuse order yang bisa dipenuhi nanti (stock masuk terus) | Nygard (premature finalization) |
| D. Backorder auto-reallocation (retry saat PutawayCompleted) | Self-healing penuh; demand menunggu supply | Sistem lebih besar (waiting-state + retry + expiry) — "terlalu jauh" utk sekarang | Richardson (saga); Hohpe & Woolf (EIP) |
| E. Izinkan wave Ready/Dispatch walau 0 task | Nol state baru | Dispatch-kosong (kirim hampa) tak bermakna; tetap perlu jalan keluar | — |
| F. Scheduled sweep cancel wave stale | Decoupled dari alokasi | Butuh background job; latensi (gantung sampai sweep); lebih kompleks dari event-driven | Nygard |
| G. Satu event `StockAllocationResultV1` gabungan (gantikan StockAllocated + Shortfall) | 1 fakta atomik; nol race antar-paruh; buang presedensi "Short menang" | **supersede** kontrak core-flow `inventory.stock_allocated.v1` stabil+ter-commit (Phase 03b/ADR-0028) = churn+risiko; payload happy-path (instruksi pick) tercampur sinyal eksepsi | EIP (1 event = 1 fakta) |

> **Catatan 2-event vs 1-event (opsi G):** dipertahankan **2 event** — `StockAllocatedV1` (outcome primer: instruksi pick + gerbang biner empty/non-empty) + `StockAllocationShortfallV1` (detail eksepsi: line kurang, untuk flag+alert). Alasan: `StockAllocated` kontrak core-flow stabil & ter-commit; split-nya sah (primary+exception) & non-breaking; satu-satunya biaya (race line parsial) ditutup aturan presedensi deterministik. Event shortfall **di-rename** dari `StockAllocationFailedV1` (ADR-0034) → `StockAllocationShortfallV1` agar akurat (mencakup line nol **dan** parsial; "Failed" terkesan total, "Partial" mengecualikan nol).

## Consequences

**Positif**
- Hang hilang **otomatis** tanpa SPV — wave nol-terpenuhi membubarkan diri; order balik antrian. Scale ke volume berapa pun (event-driven, nol scheduler).
- Order **tak hilang** (kembali `New`) → re-waveable saat stock tiba. Tak ada auto-refuse customer (aman secara bisnis).
- Deterministik & idempoten: `StockAllocatedV1` = penanda alokasi-selesai per-wave (sekali, Inbox-dedup); reset `PlaceInWave` bikin re-wave bersih (cegah `MarkLineAllocated` no-op pada line yang masih `Short` dari attempt lama).
- Wave parsial tak terganggu — kirim yang tersedia, short tetap ter-flag + ter-notif (ADR-0034).

**Trade-off / lebih sulit**
- `WaveStatus.Cancelled` baru (string-stored → **tanpa migrasi schema**; hanya nilai enum baru). UI/read-model perlu mengenali state ini.
- `StockAllocatedConsumer` bercabang (allocate vs auto-cancel) — kohesif tapi tak lagi single-purpose.
- Long-tail "takkan pernah bisa" memantul `New↔wave↔cancel` sampai expiry/backorder di-scope (di-defer).

**Yang harus dijaga**
- `Wave.Cancel()` legal HANYA dari `Active` **dan** `_pickingTaskIds.Count == 0` (invariant: wave yang sudah punya task = sedang dipenuhi, tak boleh auto-cancel). `ReleaseFromWave()` legal dari `InProgress` (clear `WaveId`).
- Race antar event satu attempt: `StockAllocationShortfall` (mark `Short`) bisa tiba sebelum/sesudah `StockAllocated`(empty)→cancel. Final konsisten karena line di-reset `Pending` di `PlaceInWave` attempt berikutnya; status line pada order ber-state `New` = sekadar hint attempt terakhir (tak dipakai sebagai invariant).
- Notifikasi wave nol-terpenuhi: TAK perlu event baru. `StockAllocationShortfallV1` TETAP menyala untuk wave nol-seluruhnya (tiap line short, allocated 0) → ia **sekaligus** jadi notifikasi "stock kurang untuk wave" via `StockAllocationShortfallNotifier`. Jadi cancel (Outbound) + alert (Notification) sama-sama dipicu sinyal yang sudah ada — nol event broker baru.
- Idempotency Inbox `(event_id, handler_type)` ([ADR-0005](0005-event-driven-outbox.md)); transisi via `Result` no-throw ([ADR-0019](0019-error-handling-result-transport-mapping.md)); `xmin` concurrency saat consumer menulis Wave+Order ([ADR-0031](0031-optimistic-concurrency-token-xmin.md)).
- Tak ada event/contract broker BARU → katalog AsyncAPI & FF#11 tak berubah (murni transisi intra-Outbound).

## Out of scope / deferred

- **Backorder** (order menunggu, auto-realokasi saat `inventory.putaway_completed.v1` tiba) — opsi D; menutup long-tail "stock akan datang". Di-defer.
- **Order-expiry / max-retry** (sweep timeout → order `Cancelled` setelah N jam/percobaan) — menutup long-tail "takkan pernah datang". Butuh scheduler. Di-defer.
- **Partial-line backorder** (kirim 10, buat order sisa 10 otomatis) — bagian dari D. Di-defer; untuk sekarang line short cukup ter-flag (ADR-0034).
- **Auto-re-waving** order backlog — tetap aksi terpisah (SPV/proses lain), bukan otomatis dari ADR ini (cegah loop mesin).
- Pemisahan `New` vs `Backordered` sebagai state order eksplisit — revisit saat D/expiry di-scope.
