## 0. UNTUK AGEN PELAKSANA — baca dulu

**Misi:** validasi alur bisnis WMS end-to-end **lewat mata-UI** (browser, Playwright MCP), temukan bug fungsional,
**perbaiki**, re-verify, dan laporkan. Target: WMS versi **lokal** matang sebelum masuk cloud.

**Peranmu = QA + Dev (fused, autonomous).** Kamu test DAN fix. Tapi disiplin lane tetap: **bukti hanya dari UI**
(rendered text + MudBlazor snackbar + Aspire Dashboard logs/traces), bukan asumsi. Saat nemu kegagalan, jangan
nge-hack workaround di test — **root-cause di kode service yang bertanggung jawab, fix, rebuild, re-run skenario
sampai hijau, tambah regression test**.

> **Pengecualian sah untuk "bukti hanya dari UI": DISCOVERY ID.** Beberapa flow WMS butuh paste-GUID (warehouse,
> order) yang **tak bisa dibaca dari UI** (lihat §2). Membaca sebuah **id** via REST/DB out-of-band = *discovery*,
> **bukan** meng-assert business behavior — itu boleh & memang perlu. Yang tetap haram: menyimpulkan PASS/FAIL
> bisnis dari sumber non-UI. Assert hasil bisnis tetap dari UI.

**Dua disiplin kardinal (membingkai semuanya):**
1. **Verifikasi HANYA dari yang terlihat.** WMS WebUI = **Blazor Server (InteractiveServer/SignalR circuit)**.
   Panggilan backend jalan **server-side** di circuit → **TAK nampak** di `browser_network_requests`/`console`
   (yang muncul cuma `_blazor` XHR + static; `favicon` 404 benign). Assert dari **teks UI ter-render + snackbar
   MudBlazor**. Sumber bukti backend yang KUAT di WMS: **Aspire Dashboard** (logs + traces + resource health per
   service) — buka ini saat menyelidiki kegagalan.
2. **Tiap efek lintas-service = asynchronous** (domain event → RabbitMQ topic `wms.events` → consumer). Perlakukan
   tiap batas service sebagai **async checkpoint**: lakukan aksi → buka halaman service hilir → **refresh sampai
   efek muncul**, timeout **~60 dtk** (di RabbitMQ lokal yang sehat biasanya **<15 dtk**). **Checkpoint mentok
   lewat timeout = SINYAL BUG, bukan "lambat saja".**

**Aturan emas saat sesuatu "kosong/gagal":** sebelum menyimpulkan bug, **rule-out** dulu (lihat §5): salah-setup
(lokasi Receiving/Quarantine belum ada → consumer DLQ), warehouse-id mismatch, circuit belum hidup, **atau
silent-401** (token TTL ~15 mnt, WebUI TANPA silent-refresh — §1.4/§4). Urut rule-out: **re-login dulu bila sudah
~12+ mnt** → cek setup (lokasi/warehouse-id) → baru simpulkan bug nyata.

---

## 1. PHASE 0 — Nyalakan stack & discovery

WMS **BUKAN docker-compose**. Ini **.NET Aspire**: satu AppHost mengorkestrasi 7 service-host + YARP gateway +
Blazor WebUI + migration-runner, plus Postgres & RabbitMQ sebagai container Docker. **Port service DINAMIS**
(Aspire assign saat runtime) — URL WebUI **tidak hardcoded**, ditemukan saat runtime.

### 1.1 Prasyarat
- **Docker Desktop HARUS jalan** (Aspire spin Postgres + RabbitMQ).
- **.NET 10 SDK** (`global.json` pin `10.0.301`; AppHost net10, service net8). `TreatWarningsAsErrors=true`
  repo-wide → warning apa pun **menggagalkan build**.

### 1.2 Up the stack (satu perintah, dari root repo)
```
dotnet run --project "C:\Users\sosro\OneDrive\Desktop\Sandboxs\src\AppHost\Wms.AppHost\Wms.AppHost.csproj"
```
Urutan otomatis: postgres/rabbitmq → **migrations (run-to-completion: migrate 7 DB + seed admin)** → auth,masterdata
→ inbound,inventory,outbound → reporting,notification → gateway → webui. Tunggu semua resource **Running/Healthy**.

> Jalankan ini lewat tool **Bash** (`run_in_background: true`) supaya stack tetap hidup sepanjang sesi.

### 1.3 Discovery (WAJIB — jangan hardcode port)
- **URL WebUI (port dinamis) — cara PALING ANDAL:** **grep stdout proses AppHost** (yang kamu jalankan
  `run_in_background` via Bash) untuk baris endpoint resource `webui` (`Now listening on: http://localhost:<port>`
  / log Aspire endpoint). **Ini lebih andal daripada men-scrape SPA dashboard.** URL itu = base semua langkah E2E.
  (`launchSettings` WebUI sebut `:58902/:58909`, tapi di bawah AppHost **di-override Aspire** — jangan dipakai langsung.)
- **Aspire Dashboard (konfirmasi/observability):** `https://localhost:17161` (profil https, default) atau
  `http://localhost:15155` (http) — endpoint dinamis + logs/traces per resource. Cert `https` = **self-signed**
  (terima warning bila dibuka via Playwright).
- **Resource Aspire (nama persis di dashboard & log — pakai ini saat triase):** `migrations` (run-to-completion),
  service `auth`·`masterdata`·`inbound`·`inventory`·`outbound`·`reporting`·`notification`, `gateway`, `webui`,
  + container `postgres`·`rabbitmq`.
- **Health per service:** `/health` (readiness) & `/alive` (liveness). **Tidak ada** `/health/live`/`/healthz`.
- **RabbitMQ Management UI** & **Postgres**: port + kredensial ada di dashboard (untuk inspeksi queue/DLQ saat fan-out mentok).

### 1.4 Login
- **Login = route root `/`** (bukan `/login`). Username field pre-filled `admin`.
- **Kredensial: `admin` / `ChangeMe123!`** (RS256 JWT). Setelah sukses: snackbar **"Login sukses."** lalu
  **redirect ke `/goods-receipts`** (tidak ada dashboard bisnis).
- **Catatan auth (penting):** route belum `[Authorize]`-gated (authZ deferred, ADR-0012). Jadi identity mengalir
  & login bekerja, tapi deep-link anon mungkin **tidak** redirect ke login. **Jangan** otomatis cap "tak ada
  guard" sebagai bug — itu by-design (deferred); cukup **catat perilaku aktual** (lihat S0). JWT disimpan di
  `TokenStore` circuit-scoped + `ProtectedLocalStorage` (`wms.access`).
- **⚠️ TTL token ~15 mnt, TANPA silent-refresh di WebUI.** Panggilan backend (yang membawa JWT) mulai **401 setelah
  ~15 mnt** walau route belum gated → muncul sebagai empty-state palsu / save no-op senyap. Putaran penuh ~45–60
  mnt → PASTI kena bila tak antisipasi. **Re-login proaktif ~tiap 12 mnt** & sebelum sub-flow panjang. Lihat §4/§5.

### 1.5 Anti-stale (Aspire ≠ docker images)
- **Tak ada image service yang di-build.** Service = proses `dotnet` biasa. `dotnet run` AppHost **otomatis
  rebuild project yang berubah**.
- **Gate build:** `dotnet build "C:\Users\sosro\OneDrive\Desktop\Sandboxs\Wms.sln"` harus **0 warning / 0 error**.
- **DATA EPHEMERAL (konsekuensi besar):** AppHost **tidak** pakai data-volume → **Ctrl+C AppHost penuh** **menghapus
  semua data** Postgres/RabbitMQ (container baru); migration re-seed **hanya admin**. ⇒ **Jalankan satu putaran E2E
  penuh dalam satu sesi AppHost.** Untuk deploy fix tanpa kehilangan data, **utamakan restart 1 service dari
  dashboard** (lihat §6 step 4). Kalau memang full-restart, kamu **WAJIB re-bootstrap master data** (Phase 1) dulu.
  RS256 key juga ephemeral per full-run → re-login.

---

## 2. PHASE 1 — Bootstrap master data (WMS TIDAK punya seed bisnis)

> ⚠️ **Kritis & WMS-spesifik.** Hanya **admin** yang di-seed. Tidak ada warehouse/product/location/stock.
> Inbound→Inventory akan **gagal/DLQ** kalau lokasi tipe **ReceivingArea** & **QuarantineArea** belum ada
> (consumer `GRConfirmed` resolve default-location via MasterData gRPC; tak ada → loud-fail → DLQ → tak ada Stock
> & PutawayTask). Jadi bootstrap ini adalah **prasyarat keras**, bukan opsional.

Buat lewat UI, **urut**. Untuk flow paste-GUID, ambil `{WH_ID}` lebih dulu (lihat di bawah):

**(a) Warehouse** — `/master-data/warehouses` → **`Tambah Warehouse`** → `Name` = `DC Jakarta`, `Address` =
apa saja → **`Simpan`** (snackbar **"Warehouse dibuat."**).
- **CAPTURE `{WH_ID}` (GUID) — TIDAK BISA dari UI.** Tabel warehouse cuma `Name | Address | Active | Actions`
  (tak ada kolom/aksi yang expose GUID); dialog create tak tampilkan id & WebUI **membuang** GUID balasan; Blazor
  Server → POST invisible di network. **Ambil via out-of-band (discovery ID — dikecualikan dari aturan UI-only, §0):**
  - **REST (disarankan):** via `Bash` curl — login `POST {gateway}/auth/login` body `{"username":"admin","password":"ChangeMe123!"}`
    → ambil `access` token → `GET {gateway}/warehouses` dengan `Authorization: Bearer <token>` → parse `id`
    warehouse `DC Jakarta`. Port `{gateway}` dari stdout AppHost / dashboard.
  - **atau DB:** query Postgres `SELECT id FROM warehouses WHERE name='DC Jakarta';` di DB masterdata (port + password di dashboard).
  - Catat `{WH_ID}` — dipakai untuk **Location** (paste GUID) & **setiap Create GR** (timpa default `WH1`).

**(b) Locations** — `/master-data/locations` → **`Tambah Location`** (4×). Field `Warehouse Id (GUID)` =
**paste `{WH_ID}`** (tidak ada dropdown — known gap; dialog validasi `Guid.TryParse`), `Type` = MudSelect,
`Code` = bebas. Buat keempat tipe:

| Code | Type (enum `LocationType`) | Fungsi |
|---|---|---|
| `REC-01`  | **`ReceivingArea`**  | tujuan default stock OnHand pasca GR-Confirm |
| `QC-01`   | **`QuarantineArea`** | tujuan stock QcHold (excluded dari outbound) |
| `RACK-A1` | `Rack`               | tujuan putaway → Available (samakan dgn pre-fill consumer, lihat S4) |
| `STG-01`  | `StagingArea`        | tujuan picking |

> Kalau S3 (fan-out) nanti mentok, **cek dulu** ReceivingArea/QuarantineArea ada **untuk `{WH_ID}` yang sama**
> dengan GR — mismatch warehouse-id ⇒ resolve gagal ⇒ DLQ.

**(c) Products** — `/master-data/products` → **`Tambah Product`**. Buat 3 SKU (set switch tracking sendiri):

| SKU | Name | UOM | Batch | Expiry | QC | Shelf Days | Peran |
|---|---|---|---|---|---|---|---|
| `SUGAR-1KG`  | Refined Sugar 1KG | PIECE | **on** | **on** | off | 365 | batch+expiry (FEFO) |
| `BEEF-500G`  | Frozen Beef 500g  | PIECE | on | on | **on** | 180 | batch+expiry + QC |
| `NAIL-CTN`   | Nails 100/ctn     | CARTON| off | off | off | — | non-batch (kontrol) |

> Master-data **create→read sinkron** (langsung muncul di list). Ini **kontrol**: kalau create→read OK di sini
> tapi gagal di service lain, masalahnya spesifik service itu — bukan platform.

**⚠️ Warehouse-id rule (WAJIB):** form **Create GR** punya field `Warehouse Id` free-text **pre-filled `WH1`**.
Lokasi Receiving/Quarantine terikat ke **`{WH_ID}` (GUID)**; consumer me-resolve lokasi default lewat gRPC dengan
`Guid.TryParse(WarehouseId)` → `"WH1"` jadi `Guid.Empty` → lokasi tak ketemu → **NotFound → DLQ** (terverifikasi).
⇒ **Setiap Create GR, TIMPA `WH1` dengan `{WH_ID}`.** Default `WH1` yang tak-valid itu sendiri **kandidat gap/bug**
untuk diperbaiki (default ke warehouse nyata / validasi) — catat di ledger; fix bila trivial.

---

## 3. PHASE 2 — Suite skenario E2E (S0–S11 + variasi)

Jalankan **berurutan** (skenario hilir butuh data hulu). Status tiap step: `PASS` ✅ · `FAIL` ❌ (+ evidence) ·
`BLOCKED` ⛔ · `N/A`. Pakai **label/route/enum WMS aktual** di tabel — itu sudah benar untuk WMS.

> **Referensi enum status WMS:** GR = `InProgress`→`Pending`→`Confirmed`/`Hold`. Stock = `Quarantine`/`OnHand`/
> `Available`/`Allocated`/`Picked`. Order = `New`/`InProgress`/`Closed`. Wave = `Active`/`Ready`/`Dispatched`.
> Putaway & Picking = `Assigned`/`Completed`.
> **Nav:** group MudNavMenu **collapsed default** — expand `MudNavGroup` (klik group) dulu sebelum klik child
> link, atau langsung `browser_navigate` ke href (route-nya tercantum tiap skenario).

### S0 — Smoke & Auth
| Step | Aksi | Expected (UI) |
|---|---|---|
| S0.1 | Buka WebUI URL `/` | Halaman Login render; field Username pre-fill `admin` |
| S0.2 | Login `admin`/`ChangeMe123!` | Snackbar **"Login sukses."** → redirect **`/goods-receipts`**; AppBar tampil `{username}` + **Logout** |
| S0.3 | Authed: deep-link + **F5** ke `/master-data/products` (full-page SSR) | Halaman **render (200)**, **bukan 500** |
| S0.4 | (info) Deep-link anon ke page | Catat perilaku aktual (authZ deferred ADR-0012 → mungkin tetap render). **Bukan auto-bug**; flag sebagai gap-by-design jika tak redirect |

> Tujuan auth: **tidak ada 500** saat full-page load/refresh/deep-link (Bug Class 5). Gating route formal =
> deferred (catat, jangan fix kecuali memang diminta).

### S1 — Master Data (kontrol; sudah dibuat di Phase 1)
| Step | Aksi | Expected |
|---|---|---|
| S1.1 | Buka Products/Warehouses/Locations | List render + 3 produk, 1 warehouse, 4 lokasi muncul |
| S1.2 | **`Tambah Product`** SKU baru (mis. `QA-SYNC-001`) → **`Simpan`** | Produk **langsung** muncul di list (write→read sinkron) |

### S2 — Inbound: GR happy-path  ·  route `/goods-receipts`
> **Create GR = panel INLINE di kiri halaman** (BUKAN dialog). Warehouse/supplier free-text.
| Step | Aksi | Expected |
|---|---|---|
| S2.1 | Panel "Create GR": `Warehouse Id` = **`{WH_ID}`** (timpa `WH1`!), `PO Ref` = `PO-S2`, `Supplier Id` = `SUP-001`, Expected Lines: `SUGAR-1KG` qty `100` (pakai **`Add line`** bila perlu) → **`Create GR`** | Snackbar "GR dibuat: {id}"; GR muncul di list (status **`InProgress`**). Klik row → `/goods-receipts/{id}` |
| S2.2 | GR Detail → **`Scan Item`** dialog: `SKU`=`SUGAR-1KG`, `Actual Qty`=`100`, `Batch`=`SUGAR-LOT-A`, `Expiry` (DatePicker, mis. akhir tahun depan), **Line Status** (radio) **`Good`** → **`Simpan`** | Tab **`Scanned (1)`**, **`Discrepancies (0)`** |
| S2.3 | Tombol **`Selesai Scan`** → konfirm dialog ("Selesai Scan?") → **`Selesai Scan`** | GR status → **`Pending`** (compile discrepancies) |
| S2.4 | **`Confirm GR`** → dialog → **`Confirm GR`** | Snackbar sukses (emits `GRConfirmed`); status → **`Confirmed`** |

> **GR state machine:** Create(`InProgress`) → Scan per-line (status `Good`/`WrongItem`/`QcHold`; dialog punya
> **`Simpan`** dan **`Simpan & Lanjut`** untuk multi-scan cepat) → **`Selesai Scan`** (page button → `Pending`) →
> **`Confirm GR`** (`Confirmed`, emits event) **atau `Hold GR`** (terminal, butuh `Reason`, **tak emit event,
> tak buat stock**). Confirm = checkpoint inbound→inventory. **CP-inbound→bus.**

### S3 — Fan-out lintas-service (Inbound→Inventory+Reporting) ⏱️ ASYNC
> Checkpoint pasca-S2.4: buka halaman hilir, refresh sampai muncul (~60s; RabbitMQ lokal biasanya <15s).
| Step | Aksi (refresh s/d muncul) | Expected |
|---|---|---|
| S3.1 | `/inventory/stocks` | Auto-row `SUGAR-1KG` @ **`REC-01`**, status **`OnHand`**, qty `100`, batch/expiry, **Source GR = GR tadi** (bukti lintas-container) |
| S3.2 | `/inventory/putaway-tasks` (clear filter status default `Assigned` bila perlu) | PutawayTask **`Assigned`** auto-created (receiving→suggested rack) |
| S3.3 | `/reporting/receiving-summary` | Baris ringkasan (Supplier/Day/GR/Received/Rejected/Disc.Rate) dengan qty=100 |

> **Mentok >60s padahal GR `Confirmed`?** Triase (urut): (1) Lokasi `ReceivingArea` ada utk `{WH_ID}`? GR pakai
> `{WH_ID}` bukan `WH1`? (2) cek **Aspire Dashboard logs** service `inventory` + **RabbitMQ DLQ** — exception/poison?
> (3) **Triangulasi**: kalau Reporting (S3.3) PUNYA tapi Inventory (S3.1) TIDAK → event terbit + 1 consumer jalan →
> break spesifik di **inventory consumer** (Bug Class 1/2; verified: kedua modul consume `GRConfirmedV1` independen).
> Kalau dua-duanya kosong → transport/publish (Bug Class 2).

### S4 — Putaway  ·  route `/inventory/putaway-tasks`
| Step | Aksi | Expected |
|---|---|---|
| S4.1 | Putaway Tasks → **`Complete`** → dialog "Complete Putaway": `Actual Destination` (pre-fill suggested **`RACK-A1`**, boleh override — destination = **free-text, TAK divalidasi** ke MasterData) → **`Complete Putaway`** | Snackbar "Stock → Available" |
| S4.2 | `/inventory/stocks` (refresh) | Stock pindah ke destination yang disubmit (**`RACK-A1`**), status **`Available`** |

### S5 — GR QcHold + Discrepancy resolution (two-axis)
| Step | Aksi | Expected |
|---|---|---|
| S5.1 | GR baru 1 line (`BEEF-500G` qty 20); **Scan** Line Status (radio) **`QcHold`** (qty=expected) → `Simpan` → **`Selesai Scan`** | GR `Pending`; **`Discrepancies (1)`** (QcHold di-flag walau qty match) |
| S5.2 | Klik page **`Confirm GR`** (membuka dialog) | Dialog tampil **peringatan** "masih ada discrepancy belum resolved"; **tombol `Confirm GR` DI DALAM dialog DISABLED** (page button sendiri tak disable) |
| S5.3 | Tab **`Discrepancies`** → **`Resolve`** → `Action` (MudSelect) = **`SendToQC (QcHold)`** → **`Simpan`** | Discrepancy → resolved |
| S5.4 | **`Confirm GR`** (ulang) | Tombol dialog enabled → GR `Confirmed` |
| S5.5 | `/inventory/stocks` (async) | Stock `BEEF-500G` status **`Quarantine`** @ **`QC-01`** — **BUKAN** `Available`, **tak ada** PutawayTask |

> **Two-axis** = (jenis line-status) × (selisih qty). `Action` (MudSelect, label verbatim):
> **`AcceptPartial (ShortDelivery)`** · **`RejectExcess (OverDelivery)`** · **`ReturnToSupplier (WrongItem)`** ·
> **`SendToQC (QcHold)`**. Varian uji:
> - **ShortDelivery** (Actual < Expected) → `AcceptPartial` → OnHand = qty aktual diterima.
> - **OverDelivery** (Actual > Expected, mis. 15 vs 10) → `RejectExcess` → OnHand = **PO qty (10)**, excess ditolak.
> - **WrongItem** → `ReturnToSupplier`. · **Hold GR** → terminal, tak buat stock.

### S6 — Outbound: Create Order  ·  route `/outbound/orders`
> WMS: dialog **`Buat Order`** TANPA warehouse & TANPA autocomplete "available N" (SKU+Qty free-text).
| Step | Aksi | Expected |
|---|---|---|
| S6.1 | **`Buat Order`** → `Customer ID`=`CUST-1`, `Ship To`=`Jakarta`, Order Lines: `SUGAR-1KG` qty `30` (**`Tambah Line`**) → **`Simpan & Buka Detail`** | Order created (status **`New`**), buka `/outbound/orders/{id}` |
| S6.2 | **CAPTURE `{ORDER_ID}`** dari URL detail (snackbar "Order dibuat: {id}" + navigate) | dibutuhkan untuk Create Wave (paste GUID) — **ini bisa dari UI** (beda dari `{WH_ID}`) |

> Order `New` **belum** reservasi stock (reservasi terjadi di Wave). Enforcement availability terjadi di **alokasi
> wave** (S7) — kalau order qty > stock, watch di S7. (Tak ada guard `insufficient_stock` di form order WMS.)

### S7 — Wave + FEFO allocation ⏱️ ASYNC  ·  route `/outbound/waves`
> WMS: dialog **`Buat Wave`** = **paste `Order IDs` (GUID, 1 per baris)** — tak ada warehouse picker / multiselect.
| Step | Aksi | Expected |
|---|---|---|
| S7.1 | **`Buat Wave`** → field `Order IDs` = paste **`{ORDER_ID}`** → **`Buat Wave`** | Wave created (status **`Active`**); buka `/outbound/waves/{id}` |
| S7.2 | `/outbound/picking-tasks` (refresh ~60s; clear filter `Assigned`) | **PickingTask `Assigned`** auto-created (SKU, qty, **batch**, source rack) |
| S7.3 | `/inventory/stocks` (refresh) | Stock yang dialokasi → **`Allocated`** (qty dikurangi dari `Available`); FEFO-split bila parsial |

> **Wave `Active` tapi PickingTask=0 + stock tak `Allocated` >60s?** Bug Class 1 (consumer alokasi salah-scope /
> read 0-rows) atau Bug Class 2 (ordered-event tak ter-publish). **Asymmetry test:** kalau fan-out (S3) jalan tapi
> alokasi (S7) tidak → curigai jalur event alokasi spesifik (mis. consumer `WaveReleased` baca FEFO list = 0 baris
> karena scope; lihat Bug Class 1). Cek **Aspire logs** service `outbound`/`inventory` + DLQ.

### S8 — Picking  ·  route `/outbound/picking-tasks`
> WMS: dialog **`Complete Picking`** hanya minta **`Staging Location (code)`** — **TAK ada field actual-qty**
> (backend belum punya). Adaptasi: complete dgn staging saja; qty = expected implisit.
| Step | Aksi | Expected |
|---|---|---|
| S8.1 | Picking Tasks → **`Complete`** → `Staging Location` = `STG-01` → **`Complete Picking`** | Snackbar sukses; task keluar dari list `Assigned` |
| S8.2 | `/inventory/stocks` (async) | Stock → **`Picked`** @ **`STG-01`** (bukan tetap `Allocated`) |
| S8.3 | `/outbound/waves/{id}` (Wave Detail) | Wave → **`Ready`** + tombol **`Dispatch Wave`** muncul |

### S9 — Dispatch + rekonsiliasi inventory ⏱️ ASYNC  ·  route `/outbound/waves/{id}`
| Step | Aksi | Expected |
|---|---|---|
| S9.1 | Wave Detail → **`Dispatch Wave`** → dialog → **`Dispatch Wave`** | Wave → **`Dispatched`**; Order → **`Closed`**; emits `ShipmentDispatched` |
| S9.2 | `/inventory/stocks` (refresh ~60s) | Stock ter-ship **HILANG** dari inventory; **total on-hand turun = qty ship** |

> **Wave `Dispatched` & order `Closed` TAPI stock ship masih ada (`Allocated`/`Picked`)?** = **inventory
> overstated** = bug rekonsiliasi (Bug Class 3). Akar historis: Complete-Picking tak transisi stock
> `Allocated→Picked` (consumer by-id read return null karena scope) → dispatch "hapus Picked" tak nemu apa-apa.

### S10 — FEFO ordering (multi-lot) 🎯
**Pre:** 2 lot SKU sama beda expiry, dua-duanya `Available`. Buat lewat **2 GR** `SUGAR-1KG`: batch `SUGAR-LOT-A`
exp **lebih awal**, batch `SUGAR-LOT-B` exp **lebih lambat** (masing-masing confirm + putaway → Available).
| Step | Aksi | Expected |
|---|---|---|
| S10.1 | Order qty ≤ lot-awal → wave → cek PickingTask | Alokasi **dari lot expiry paling awal** (batch = `SUGAR-LOT-A`) |
| S10.2 | (opsional) Order qty > lot-awal (span 2 lot) | Lot-awal **diexhaust dulu**, sisa dari lot berikut (FEFO + split benar) |
| S10.3 | `/inventory/stocks` | Lot-awal berkurang/Allocated dulu; lot-akhir **untouched** s/d lot-awal habis; **total konservasi** |

### S11 — Konservasi stok (cross-cutting)
| Step | Cek | Expected |
|---|---|---|
| S11.1 | Jumlahkan semua qty `SUGAR-1KG` lintas semua status (Available+Allocated+Picked+Quarantine+OnHand) sebelum vs sesudah tiap aksi | Total **konsisten** KECUALI: **+** saat GR Confirm (barang masuk), **−** saat Dispatch (barang keluar). Tak ada qty bocor/ganda |

### + GR Attachment (upload)  ·  GR Detail → tab `Attachments (N)`
> WMS: dialog **`Upload Attachment`** (format **PDF/JPG/PNG/WEBP, maks 50 MB**), tombol **`Pilih File`** + **`Upload`**.
> ⚠️ **Download = GAP** (tak ter-wire di WMS). Storage lokal = filesystem (`LocalObjectStore`).
| Step | Aksi | Expected |
|---|---|---|
| A.1 | Buat PNG valid kecil (lihat §4 mekanik) → tab Attachments → **`Upload Attachment`** → **`Pilih File`** (file chooser) → `browser_file_upload` abs-path → **`Upload`** | Row attachment muncul (File/Tipe/Ukuran/Uploaded); tab jadi `Attachments (1)` |
| A.2 | Verifikasi persist | Reload GR Detail → row tetap ada. (Download UI = gap; opsional verifikasi blob di disk / atau **catat download sebagai gap untuk diperbaiki**, lihat §6) |

---

## 4. Mekanik operasional & gotchas (Playwright + MudBlazor + Aspire)

**Evidence model (Blazor Server — fondasi):** call backend invisible di browser network/console. **Sumber bukti:**
1) **teks UI ter-render**; 2) **snackbar MudBlazor** (role `alert`) — error backend sering muncul lengkap di sini;
3) **Aspire Dashboard** → tab **Console/Structured logs** + **Traces** per resource (di sini kamu lihat exception,
stack, service mana). Saat fan-out mentok → **RabbitMQ Management** (queue depth, **DLQ**).

**Token economy:** baca tabel/status pakai `browser_evaluate` (`[...document.querySelectorAll('table tbody tr')]
.map(tr=>[...tr.querySelectorAll('td')].map(td=>td.innerText.trim()).join(' | ')).join('\n')`) — ~5× lebih murah
dari snapshot besar. Snapshot **hanya** saat butuh `ref` element untuk klik.

**Kontrol MudBlazor (jebakan umum):**
- **MudSelect** (Location **Type**, Resolve **Action**, dan **Status filter** di halaman list) = input **readonly**
  → `fill()` GAGAL. **Klik parent div untuk buka → klik option** (`.mud-popover .mud-list-item:has-text("...")`).
- **Scan-dialog `Line Status` = MudRadioGroup** (BUKAN MudSelect): klik **radio** berlabel `Good`/`WrongItem`/`QcHold`
  langsung — **tak ada popover**.
- **MudNumericField** (Qty): `fill('100')` commit binding. Verifikasi value sebelum submit.
- **MudDatePicker** (Expiry) = readonly → klik **"Open Date Picker"** → klik judul bulan/tahun untuk lompat
  (judul → grid bulan → pilih). **Popup overflow viewport** → `browser_resize` lebih tinggi (mis. 1500×1800)
  supaya tanggal/tombol bawah ter-klik.
- **Snapshot ref jadi stale** pasca re-render → **re-snapshot** sebelum klik. **Prefer CSS selector stabil**
  (`a[href=...]`, `button:has-text("...")`) — tahan re-render SignalR; `ref` element tidak.
- **Tombol Save sering disabled** sampai precondition (Confirm GR di dialog disabled bila ada discrepancy; Upload
  disabled s/d file valid; Create Order disabled bila 0 line).

**Nav MudNavMenu:** group **collapsed default** → klik `MudNavGroup` (toggle) dulu, lalu child link. Lebih
sederhana: `browser_navigate` langsung ke route. Bila klik link timeout "subtree intercepts pointer events" =
group ke-collapse → toggle ulang.

**Attachment mechanic:** buat PNG valid via Bash, taruh di path **Windows pasti** (mis. folder repo), lalu
`browser_file_upload` dengan **absolute path**. Contoh bikin PNG 1×1:
```
printf '\x89PNG\r\n\x1a\n\x00\x00\x00\x0dIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89\x00\x00\x00\x0dIDATx\x9cc\xf8\xcf\xc0\x00\x00\x00\x03\x00\x01\xe2!\xbc3\x00\x00\x00\x00IEND\xaeB`\x82' > /c/Users/sosro/OneDrive/Desktop/Sandboxs/.qa-attach.png
```
(`.txt` ditolak filter format). Alur: **`Pilih File`** (buka chooser) → `browser_file_upload` abs-path → **`Upload`**.

**Async checkpoint discipline:** ~60s lokal. "Refresh until effect appears" → klik tombol **`Apply`** filter
(re-query di tempat), atau nav keluar+balik via in-app link. Checkpoint mentok lewat timeout = sinyal bug.

**Data ephemeral & restart:** **Ctrl+C AppHost penuh = SEMUA data hilang** (no volume → container baru). Untuk
deploy fix, **utamakan restart 1 service dari Aspire Dashboard** (data seed selamat — lihat §6 step 4); full
restart hanya untuk perubahan AppHost/migrations/shared, dan **setelahnya re-bootstrap Phase 1**.

**Silent-401 (BERLAKU di lokal — token TTL ~15 mnt, TANPA silent-refresh di WebUI).** Setelah ~15 mnt, panggilan
backend (yang bawa JWT) mulai **401** walau route belum gated → submit bisa "diam tak terjadi apa-apa", atau hasil
**menyesatkan**: empty-state palsu ("stock kosong"), save no-op senyap, "data tak ditemukan". Putaran S0–S11 penuh
~45–60 mnt → **PASTI** kena bila tak antisipasi. **Rule-out WAJIB:** saat aksi yang datanya ADA tiba-tiba lapor
kosong / save no-op / kamu sudah ~12+ mnt → **re-login dulu** (nav `/` → login ulang) **sebelum** menyimpulkan bug
scope/setup. **Re-login proaktif ~tiap 12 mnt** sebelum sub-flow panjang (GR cycle, wave→pick→dispatch). Ini
jebakan #1 yang dulu salah-didiagnosa sebagai bug warehouse-scoping (TomWMS TCK-002).

---

## 5. Kelas bug berulang (di mana defect nyata bersembunyi) + rule-out

Sebelum cap "bug", **rule-out** urut: **(0) silent-401** — sudah ~12+ mnt, ATAU aksi yang datanya-ADA mendadak
lapor kosong / save no-op? → **re-login dulu** (§4) lalu ulang. **(a)** lokasi Receiving/Quarantine untuk `{WH_ID}`
ada? **(b)** warehouse-id GR = `{WH_ID}` (bukan `WH1`)? **(c)** circuit hidup (reload bila perlu)? **(d)** cek
Aspire logs + RabbitMQ DLQ. Lalu klasifikasi:

**Bug Class 1 — 🔴 Warehouse-scoping / s2s identity (paling sering di TomWMS, fixed 4×).**
Service host **dual-role** (HTTP API + bus-consumer) daftar `ICurrentUser=HttpContextCurrentUser` untuk **dua**
scope. EF global query-filter: `IsSystemAdmin || allowedWarehouseIds.Contains(WarehouseId)`. Request dgn JWT
end-user (REST/dropdown) → `IsSystemAdmin=true`, lihat data. Tapi jalur **s2s (gRPC/command) atau consumer scope
tanpa JWT end-user** → resolve principal **unauthenticated** (BUKAN system) → `allowedWarehouseIds=[]` → filter
**buang semua row** → sisi s2s lihat **0** → silent failure.
- **Gejala E2E:** *"satu halaman/dropdown menunjukkan data ADA, tapi aksi lintas-service bertindak seolah kosong"*
  → 404 / `insufficient_stock` / tak ada alokasi / tak ada artefak hilir / stock ship tak terhapus.
- **Titik rawan (tiap fix nambal SATU entry-point → read baru di consumer path bisa kambuh):** gRPC read-port
  (validasi CreateOrder); FEFO/list read di consumer (alokasi wave); **by-id read di consumer** (`FindAsync`/by-id
  **TETAP kena global filter** — jangan asumsi bypass; verifikasi); GR read bila `RoleClaimType` tak di-set
  (`IsInRole("ADMIN")` tak match).
- **Real vs false:** input kecil (qty=1) **tetap** gagal ⇒ scope memang lihat 0; cross-check halaman authoritative
  (list/dropdown) menunjukkan data. ⇒ Bug Class 1. (Tapi **rule-out silent-401 dulu** — gejalanya mirip!)

**Bug Class 2 — ⏱️ Async transport tak ter-wire / event tak terkirim.**
Event tak sampai → checkpoint tak pernah selesai. Sub-kasus: bus in-process tak lintas-container; ordered-event
tak ter-publish (mis. flag ordering hilang → `WaveReleased`/`StockAllocated` poison sementara `GRConfirmed`
unordered jalan = **asimetri fan-out-jalan-tapi-alokasi-tidak**); channel tak ter-provision jadi topic. **Real
vs false:** asymmetry-triangulation — kalau satu jenis event jalan & lain tidak, curigai jalur ordered/spesifik;
cek DLQ. Di WMS lokal RabbitMQ topic `wms.events` — cek binding/queue di Management UI.

**Bug Class 3 — 🧮 Konservasi/rekonsiliasi.** Dispatch tinggalkan stock ship on-hand → inventory overstated.
Muncul saat transisi status (`Allocated→Picked→removed`) tak lengkap (sering gejala hilir Bug Class 1 di consumer
read). **Validasi** matematika S9/S11: total konservasi kecuali +GR-Confirm / −Dispatch.

**Bug Class 4 — 🔁 Idempotency / double-effect.** Event retry/duplikat tak boleh menggandakan stock/task. Kolaps
terkait: `processed_inbox` shared antar consumer dgn inbox-key `(MessageId,eventType)` identik → PK collision →
cuma 1 dari N consumer commit (silent loss, DLQ=0). **Validasi:** total tak berganda saat retry.

**Bug Class 5 — 🖥️ Auth state-guards (deep-link/refresh).** Authed refresh harus **render 200, bukan 500** (Local:
BFF tanpa HTTP auth-scheme → SSR challenge → 500). Di WMS authZ deferred (ADR-0012) → fokus: **tidak ada 500**.
(Gating route formal = catat sbg gap-by-design.)

---

## 6. Loop AUTONOMOUS-FIX (saat bug DIKONFIRMASI)

Setelah rule-out (§5) dan yakin ini **bug fungsional** (bukan silent-401/setup/known-gap):

**1. Lokalisasi.** Pakai **Aspire Dashboard logs/traces** (service mana exception/poison) + snackbar + hasil
triangulasi/asymmetry → tentukan service + entry-point. Modul ada di `src/Modules/<Context>/Wms.<Context>.*`
(`.Domain` logika+entity, `.Application` handler, `.Infrastructure` repo/DbContext, `.Api` endpoint, `.Contracts`
integration-event, `.Grpc` server bila ada). Gateway: `src/Gateway/Wms.Gateway`. Platform/adapters:
`src/Platform/Wms.Platform.Local`. BuildingBlocks shared: `src/BuildingBlocks/*`.

**2. Fix di kode** (.cs) pada entry-point spesifik. Pertahankan idiom Clean Architecture repo (Domain murni;
Application orchestrate; Infrastructure I/O). **Komentar prosa Indonesia** (label What/Why/How), istilah teknis EN.

**3. Build green (gate):**
```
dotnet build "C:\Users\sosro\OneDrive\Desktop\Sandboxs\Wms.sln"
```
**0 warning / 0 error** (TreatWarningsAsErrors + Nullable enable). Warning = build gagal — bereskan dulu.

**4. Re-deploy — UTAMAKAN cara hemat-data:** edit `.cs` satu service → **restart HANYA service itu dari Aspire
Dashboard** (tombol Restart per-resource; atau `dotnet watch` di host itu). Ini rebuild proses itu saja dan
**mempertahankan data + seed Postgres/RabbitMQ** (container tak diturunkan).
> ⚠️ **Hanya Ctrl+C AppHost penuh yang MENGHAPUS data** (no volume → container baru). Pakai full restart **hanya**
> untuk perubahan di AppHost / migrations / shared `BuildingBlocks` / `Platform`. Setelah full restart →
> **re-bootstrap Phase 1** dulu. Hindari full restart per-fix (re-bootstrap master-data = time-sink terbesar).

**5. Re-verify** skenario yang gagal **dengan ID nyata yang baru di-scrape** (bukan GUID hardcoded) → harus
**hijau** end-to-end.

**6. Regression test.** Project test **flat di bawah `tests/`** (mis. `tests/Wms.Inventory.Domain.UnitTests`,
`tests/Wms.Inventory.IntegrationTests`; **Reporting hanya `.IntegrationTests`**, tak ada Domain.UnitTests). Tambah
test yang **gagal tanpa fix, lulus dengan fix** (mis. Bug Class 1: assert read user-scope = 0 sementara system-scope = full).
```
dotnet test "C:\Users\sosro\OneDrive\Desktop\Sandboxs\Wms.sln"
```

**7. Catat** di ledger (§8).

### Pola fix yang sering (kenali cepat)
- **Bug Class 1 fix:** tambah method repo `*ForSystemAsync` pakai **`IgnoreQueryFilters()`**, dipanggil **HANYA**
  dari entry-point system/event-driven (gRPC RPC, consumer handler), di-scope param eksplisit (`warehouseId`/
  `waveId`) dari event tepercaya. **Biarkan jalur REST/user tetap ter-filter** (defense-in-depth — **jangan** override
  global `ICurrentUser`→system, atau tiap user REST jadi cross-warehouse = kebocoran). Domain math (FEFO/split)
  **jangan disentuh** — ini bug **visibilitas row** murni. Root-fix arsitektural (consumer-scope = system identity,
  diskriminator **`HttpContext is null`** BUKAN `!IsAuthenticated`) = blast-radius besar → **eskalasi/tiket
  terpisah**, jangan kerjakan diam-diam.
- **Outbox event tak ke-registry** → GR Confirm rollback/stuck `Pending`. Registry dari assembly loaded-only bisa
  miss `*.Contracts` lazy → pastikan reference closure ter-scan.
- **State-machine / status enum salah** → perbaiki transisi di Application handler + test.
- **Konservasi (Bug Class 3)** → biasanya akar di consumer read (Bug Class 1) atau handler transisi tak commit.

### Kapan FIX vs CATAT-gap vs ESKALASI
- **FIX sekarang:** defect fungsional yang merusak alur bisnis / integritas data — fan-out tak terkirim, scope
  0-rows, konservasi bocor, idempotency ganda, 500 pada aksi normal, FEFO salah urut, state-machine salah,
  resolusi discrepancy salah.
- **CATAT sebagai known-gap** (fix hanya bila trivial / memblokir skenario): gap UI terdokumentasi WMS — Location
  warehouse paste-GUID (no dropdown); Create Wave paste order-GUID (no eligible-order multiselect); Create Order
  no-warehouse + no availability-autocomplete; Complete Picking no-actual-qty; **Attachment download belum
  ter-wire**; Receiving Summary no period-filter; Adjust Stock reason tak persist; GR-create default `WH1`.
  Ini gap UX/fitur, bukan bug korektness. Catat semua; perbaiki bila cepat atau bila memblokir suite.
- **ESKALASI/STOP:** butuh keputusan arsitektur blast-radius besar (root-fix consumer-identity), infra yang belum
  ada, atau stack tak bisa sehat. Dokumentasikan jelas + berhenti — **jangan nge-hack**.

---

## 7. Definition of Done

- Stack up via Aspire; WebUI ditemukan (grep stdout) & login OK.
- Phase 1 bootstrap lengkap (warehouse + `{WH_ID}` di-capture + 4 lokasi tipe benar + 3 produk).
- **S0–S11 + FEFO multi-lot + QcHold two-axis + Over/ShortDelivery + Attachment upload + konservasi** dieksekusi.
- Tiap bug fungsional yang ditemukan: **root-caused → fixed → build green → re-verified hijau → regression test
  ditambah**. Known-gap dicatat. Yang dieskalasi didokumentasikan dengan alasan.
- Ledger akhir (§8) terisi: matriks scenario × hasil + daftar bug (found/fixed/escalated) + daftar known-gap.

---

## 8. Ledger / laporan (jaga sepanjang sesi)

Maintain tabel ringkas (tulis ke file mis. `Sandboxs/docs/e2e/E2E_Run_<tanggal>.md` atau langsung di ringkasan akhir):

```
## E2E Run — WMS local — <UTC date> — commit <sha>

### Matriks skenario
| Skenario | Hasil | Catatan |
| S0 auth/smoke      | ✅/❌ | |
| S1 master-data     | ✅/❌ | |
| S2 GR happy        | ✅/❌ | |
| S3 fan-out 3/3     | ✅/❌ | |
| S4 putaway         | ✅/❌ | |
| S5 QcHold/disc     | ✅/❌ | |
| S6 order           | ✅/❌ | |
| S7 wave+FEFO       | ✅/❌ | |
| S8 picking         | ✅/❌ | |
| S9 dispatch+reconcile | ✅/❌ | |
| S10 FEFO multi-lot | ✅/❌ | |
| S11 konservasi     | ✅/❌ | |
| + Attachment upload| ✅/❌ | download = known-gap |

### Bug ditemukan & diperbaiki
| # | Skenario | Gejala | Akar (service/file) | Fix (commit) | Bug Class | Regression test |

### Known-gaps (dicatat, tak/diperbaiki)
| Gap | Lokasi | Severity | Action |

### Eskalasi (butuh keputusan)
| Isu | Kenapa eskalasi | Rekomendasi |
```

> **Disiplin pelaporan:** lapor jujur — kalau skenario gagal, tulis gejala + evidence (teks UI/snackbar/Aspire log).
> Klaim "fixed" hanya setelah **build green + re-verify hijau dengan ID nyata**. Jangan klaim done yang vacuous
> (mis. "passed" padahal karena GUID hardcoded 404). Bukti sebelum asersi, selalu.

---

### Lampiran — peta route & label WMS (quick reference)
- Login `/` · GR list+create `/goods-receipts` · GR detail `/goods-receipts/{id}`
- Master Data: `/master-data/products` · `/master-data/warehouses` · `/master-data/locations`
- Inventory: `/inventory/stocks` · `/inventory/putaway-tasks`
- Outbound: `/outbound/orders` · `/outbound/orders/{id}` · `/outbound/waves` · `/outbound/waves/{id}` · `/outbound/picking-tasks`
- Reporting: `/reporting/receiving-summary` · `/reporting/stock-on-hand` · `/reporting/dispatch-summary` · `/reporting/operator-activity` · Notifications `/notifications`
- Tombol kunci (Indonesia): `Tambah …`, `Buat …`, `Simpan`, `Batal`, `Create GR`, `Add line`, `Scan Item`,
  `Simpan & Lanjut`, `Selesai Scan`, `Confirm GR`, `Hold GR`, `Resolve`, `Upload Attachment`, `Pilih File`,
  `Upload`, `Complete`, `Complete Putaway`, `Buat Order`, `Simpan & Buka Detail`, `Buat Wave`, `Complete Picking`,
  `Dispatch Wave`, `Apply`, `Reset`, `Kembali ke List`.
```
