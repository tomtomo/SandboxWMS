# TomSandboxWMS

> Sumber acuan awal untuk **TomSandboxWMS** — *Warehouse Management System*. Dokumen ini mencakup **spesifikasi alur bisnis modul** (Inbound, Inventory, Outbound, plus supporting context).

Tujuan Project: Peta belajar untuk **1. Enterprise standart software design and Architecture, 2. AZ-204(Azure) & 3. Professional Cloud Developer(GCP)** dengan `TomSandboxWMS` sebagai sandbox praktek.

---
# Modul Detail

## Ringkasan

Bayangkan satu palet barang masuk ke gudang. Ia harus **diterima**, dicatat, lalu **disimpan ke rak**. Setelah tersedia, barang itu menunggu **order dari customer**, lalu **diambil**, dimuat ke truck, dan **dikirim keluar**. WMS mengatur seluruh perjalanan ini.

Sistem dibagi menjadi **3 core modul** dan **4 supporting modul** (bounded context) yang tidak saling memanggil langsung, melainkan berkomunikasi lewat **domain event** (pesan peristiwa antar-modul):

```
Inbound  ──event: GRConfirmed──▶  Inventory  ◀──events: WaveReleased / ShipmentDispatched──  Outbound
                                       │
                                       └──event: StockAllocated──▶ Outbound
```

Alur inti: **terima barang → simpan ke rak → stok tersedia → terima order → alokasi → picking → kirim**.

> **Konvensi penulisan field:** field audit standar (`createdBy`, `createdAt`, `modifiedBy`, `modifiedAt`, dan timestamp transisi state) di-handle oleh interface `IAuditable` di level infrastruktur dan **tidak ditulis eksplisit** di spec field tiap aggregate. Yang ditulis hanya field yang punya makna domain. Hal yang sama berlaku untuk metadata event (eventId, occurredAt, causedBy) — di-handle oleh event envelope, bukan payload.

---

## Core Modul

### A — Inbound

| Aggregate | Definisi |
|---|---|
| `GoodsReceipt` (state: InProgress, Pending, Confirmed, Hold) | Dokumen penerimaan barang multi-SKU dari satu pengiriman supplier ke gudang |
| `GRAttachment` | Dokumen pendukung GR (PDF/image ASN, PO, proof-of-delivery) yang di-upload operator; byte di object storage, metadata + blob path di row. Aggregate root **terpisah** dari `GoodsReceipt` (tertaut via `goodsReceiptId`), **tidak** memancarkan event lintas-modul |

| Event | Pemancar | Penerima | Payload inti | Efek di penerima |
|---|---|---|---|---|
| `GRConfirmed` | Inbound | Inventory | `grId`, `poRef`, `warehouseId`, `receivedLines[]`, `rejectedLines[]` | Per line di `receivedLines`: create Stock (Good → `OnHand`, QcHold → `Quarantine`) dan create `PutawayTask` untuk yang OnHand. `rejectedLines` tidak menambah stok. |

#### Spesifikasi Aggregate

GoodsReceipt mengelola seluruh siklus penerimaan **satu pengiriman barang** (satu surat jalan / satu truk). Satu GR dapat berisi banyak SKU sekaligus.

**State & Field**

| State | Field yang dikelola | Keterangan |
|---|---|---|
| **InProgress** | `grId`, `poRef`, `supplierId`, `warehouseId`, `dockDoor`, `expectedLines[]`, `scannedLines[]` | SPV buat header → `expectedLines` di-snapshot dari PO (per SKU: `expectedQty`, `uom`). Tiap entry di `scannedLines` berisi `sku`, `actualQty`, `batch`, `expiry`, `lineStatus`. Berlangsung dari header dibuat sampai operator declare selesai scan. |
| **Pending** | + `quantityChecks[]`, `discrepancies[]` | Sistem auto-hitung `quantityChecks` per SKU (variance: `Normal` / `ShortDelivery` / `OverDelivery`). `discrepancies[]` di-compile dari (a) quantityCheck dengan variance ≠ Normal dan (b) scannedLine dengan lineStatus ≠ Good. Menunggu keputusan SPV. |
| **Confirmed** | + `resolutions[]` | SPV approve. Setiap discrepancy harus punya pasangan resolution (`action`, `note?`). Terminal — read-only. Emit `GRConfirmed`. |
| **Hold** | + `holdReason` | SPV reject seluruh GR (alasan berat: dokumen bermasalah, lot tercampur, perselisihan supplier). Tidak emit event. Terminal untuk scope ini. |

**Konsep Two-Axis Discrepancy**

Discrepancy lahir dari **dua sumbu yang independen** — bukan satu enum tunggal:

- **`lineStatus`** (per scan, di-tag operator): `Good` · `WrongItem` · `QcHold`
- **`quantityVariance`** (per SKU, dihitung sistem saat scan selesai): `Normal` · `ShortDelivery` · `OverDelivery`

Satu SKU bisa kena dua sumbu sekaligus — misal datang 100 carton (`OverDelivery`) tapi 5 di antaranya rusak (`QcHold`). Dimodelkan sebagai dua entry discrepancy terpisah, masing-masing dengan resolution sendiri.

**Catatan implementasi:** notasi `expectedLines[]`, `scannedLines[]`, `discrepancies[]` di sini adalah representasi konseptual aggregate. Di database, struktur ini di-normalize jadi tabel terpisah (`gr_expected_lines`, `gr_scanned_lines`, `gr_discrepancies`) dengan FK ke `goods_receipts`. Aggregate tetap satu kesatuan di domain model — mapping ke tabel di-handle oleh ORM.

#### Flow

**A1 · SPV input GR header;**
Barang tiba di gudang. SPV pilih PO yang akan diterima → sistem snapshot daftar SKU + expected qty ke `expectedLines`. SPV set `dockDoor` dan `supplierId`.
`GoodsReceipt: (none) → InProgress`

**A2 · Operator scan barang;**
Operator scan per carton/line. Tiap scan capture `sku`, `actualQty`, `batch`, `expiry`, dan operator tag `lineStatus` (Good / WrongItem / QcHold). Proses bisa multi-session — operator boleh berhenti dan lanjut tanpa mengubah state.
`GoodsReceipt: InProgress` (tetap)

**A3 · Operator declare scan selesai;**
Operator submit "Scan Complete". Sistem auto-hitung `quantityChecks` per SKU dan compile `discrepancies[]` (gabungan quantity variance ≠ Normal + line lineStatus ≠ Good).
`GoodsReceipt: InProgress → Pending`

**A4 · SPV review GR;**
SPV buka GoodsReceipt, lihat ringkasan expected vs actual dan daftar discrepancy yang ter-group per SKU/type. Alur bercabang:

- ✗ **Hold.** Seluruh GR tidak diterima. Inbound **tidak** memancarkan event apa pun.
  `GoodsReceipt: Pending → Hold`. **Berhenti (belum ditentukan alur bisnis selanjutnya)**

- ✓ **Confirm.** SPV resolve setiap discrepancy dengan `action` sesuai type-nya. Default SOP umum:

  | Discrepancy type | Action default | Efek di event payload |
  |---|---|---|
  | `ShortDelivery` | `AcceptPartial` | `actualQty` masuk `receivedLines`; sisa keluar dari scope GR ini |
  | `OverDelivery` | `RejectExcess` | qty sesuai PO masuk `receivedLines`; excess masuk `rejectedLines` |
  | `WrongItem` | `ReturnToSupplier` | line masuk `rejectedLines` |
  | `QcHold` | `SendToQC` | line masuk `receivedLines` dengan `status: QcHold` |

  Setelah semua discrepancy memiliki resolution, SPV post GR. Inbound memancarkan event `GRConfirmed` dengan payload `receivedLines[]` + `rejectedLines[]`.
  `GoodsReceipt: Pending → Confirmed` **selesai**

#### Implikasi ke modul lain

1. **Inventory** memiliki state `Quarantine` di `Stock` untuk handle line `QcHold` (lihat section B).
2. **QC sebagai konteks** masih implisit. Saat ini `QcHold` cuma menempatkan barang di Quarantine — flow inspeksi QC + keputusan release/reject belum di-scope.
3. **Return-to-vendor** untuk `rejectedLines` adalah proses keluar barang dengan dokumen sendiri. Untuk scope sekarang cukup tercatat sebagai metadata di event `GRConfirmed`; flow detail-nya menyusul.

#### Spesifikasi Aggregate — GRAttachment

`GRAttachment` menyimpan dokumen pendukung satu `GoodsReceipt` (surat jalan / ASN, PO, foto proof-of-delivery). **Byte content** disimpan di **object storage**; row attachment hanya menyimpan **metadata + blob path**. Ini aggregate root tersendiri (bukan child `GoodsReceipt`) — punya repository + tabel sendiri (`inbound.gr_attachments`) supaya operator bisa upload banyak dokumen bertahap tanpa full-load GR. Tautan ke GR bersifat **logical FK** (`goodsReceiptId`), tanpa navigation property.

**Field**

| Field | Keterangan |
|---|---|
| `attachmentId`, `goodsReceiptId`, `fileName`, `contentType`, `sizeBytes`, `blobPath`, `uploadedAt` | Dibuat saat operator upload dokumen: byte ditulis ke object storage, metadata + path disimpan di row. Tidak punya state machine — sekali ter-upload bersifat immutable (kecuali soft-delete). |


**Invariant (factory `GRAttachment.Create`):** `fileName` wajib & ≤ 256 char; `contentType` wajib & ada di whitelist (`application/pdf`, `image/jpeg`, `image/jpg`, `image/png`, `image/webp`); `sizeBytes` > 0 dan ≤ **50 MB**; `blobPath` wajib (pola `{grId}/{attachmentId}/{fileName}`).

**Catatan implementasi:** tabel `inbound.gr_attachments` di-index per `goodsReceiptId`. Byte tidak pernah masuk Database — row hanya menyimpan metadata + `blobPath`.

---

### B — Inventory

| Aggregate | Definisi |
|---|---|
| `Stock` (state: Quarantine, OnHand, Available, Allocated, Picked) | Balance fisik per `(SKU, Location, Batch)` dengan status lifecycle |
| `PutawayTask` (state: Assigned, Completed) | Tugas memindahkan stock dari area receiving ke rak |

| Event | Pemancar | Penerima | Payload inti | Efek di penerima |
|---|---|---|---|---|
| `StockAllocated` | Inventory | Outbound | `waveId`, `allocations[]: { sku, locationId, batch, qty, stockId }` | Create `PickingTask` per entry `allocations[]`. |

#### Spesifikasi Aggregate

**`Stock`** — balance fisik per `(sku, locationId, batch)`. Satu kombinasi unik = satu Stock record.

**State & Field**

| State | Field yang dikelola | Keterangan |
|---|---|---|
| **Quarantine** | `stockId`, `sku`, `locationId` (quarantine area), `batch`, `expiry`, `qty`, `sourceGrId` | Stock berasal dari GR dengan `lineStatus=QcHold`. Tidak available untuk allocation. Menunggu keputusan QC (release ke OnHand, atau reject — out of scope). |
| **OnHand** | `stockId`, `sku`, `locationId` (receiving area), `batch`, `expiry`, `qty`, `sourceGrId` | Stock baru masuk via `GRConfirmed` dengan `lineStatus=Good`. Belum di-putaway. Tidak available untuk allocation. |
| **Available** | + `locationId` berubah ke rack | Sudah di rak. Free untuk dialokasi ke Wave. |
| **Allocated** | + `allocatedToWaveId` | Sudah direservasi untuk Wave tertentu. Tidak boleh double-allocate. Tetap di rak secara fisik. |
| **Picked** | + `locationId` berubah ke staging area, `pickingTaskId` | Sudah diambil dari rak, di staging area, menunggu dispatch. Akan dihapus saat `ShipmentDispatched`. |

**`PutawayTask`** — instruksi memindahkan satu Stock unit dari area receiving ke rak.

**State & Field**

| State | Field yang dikelola | Keterangan |
|---|---|---|
| **Assigned** | `taskId`, `stockId`, `sourceLocationId` (receiving), `suggestedDestinationId` (rack), `assignedTo` | Dibuat saat `GRConfirmed`. Suggested destination dihitung oleh putaway strategy (default: closest empty bin sesuai zona SKU). `assignedTo` = operator yang ditugaskan. |
| **Completed** | + `actualDestinationId` | Operator scan stock + scan destination location. Stock pindah lokasi + state `OnHand → Available`. |

**Catatan:** PutawayTask hanya dibuat untuk Stock dengan state `OnHand`. Stock `Quarantine` tidak generate PutawayTask karena tidak boleh masuk rack reguler.

#### Flow

**B1 · Inventory menerima `GRConfirmed`;**
Untuk setiap entry di `receivedLines`:
- `status: Good` → create Stock di receiving area dengan state `OnHand`, lalu create PutawayTask
- `status: QcHold` → create Stock di quarantine area dengan state `Quarantine`, **tidak** create PutawayTask

`Stock: (none) → OnHand atau Quarantine` · `PutawayTask: Assigned` (hanya untuk OnHand)

**B2 · Operator putaway;**
Operator scan stock di receiving area, sistem tampilkan suggested destination. Operator pindah barang ke rak, scan destination location. PutawayTask completed, Stock pindah lokasi dan state berubah.
`PutawayTask: Assigned → Completed` · `Stock: OnHand → Available`

#### Transisi state Stock yang dipicu modul lain

| Trigger | Transisi |
|---|---|
| Event `WaveReleased` dari Outbound | `Available → Allocated` (lihat C3) |
| `PickingTask: Assigned → Completed` di Outbound | `Allocated → Picked` (lihat C5) |
| Event `ShipmentDispatched` dari Outbound | `Picked → (removed)` (lihat C6) |

#### Implikasi ke modul lain

1. **QC release flow** (`Quarantine → OnHand`) belum di-scope. Saat di-scope nanti, kemungkinan butuh aggregate baru (`QCInspection`) di modul terpisah.
2. **Allocation failure** — jika stock Available tidak cukup untuk memenuhi `WaveReleased`, behavior saat ini implisit (partial allocation atau reject seluruh wave?). Perlu di-spec eksplisit; out of scope di iterasi ini.
3. **Putaway strategy** (chaotic vs fixed vs ABC) di-treat sebagai konfigurasi internal modul Inventory, tidak terlihat dari luar.

---

### C — Outbound

| Aggregate | Definisi |
|---|---|
| `OutboundOrder` (state: New, InProgress, Closed) | Order pengiriman dari customer, multi-SKU |
| `Wave` (state: Active, Ready, Dispatched) | Grouping OutboundOrder yang diproses dan di-dispatch bersama |
| `PickingTask` (state: Assigned, Completed) | Tugas mengambil stock dari satu lokasi rak ke staging area |

| Event | Pemancar | Penerima | Payload inti | Efek di penerima |
|---|---|---|---|---|
| `WaveReleased` | Outbound | Inventory | `waveId`, `lines[]: { orderId, sku, qty }` | Per line: pilih stock `Available` sesuai strategy (default FEFO), mark sebagai `Allocated` ke wave ini. Emit `StockAllocated`. |
| `ShipmentDispatched` | Outbound | Inventory | `waveId` | Remove semua Stock dengan state `Picked` yang terikat ke `waveId`. |

#### Spesifikasi Aggregate

**`OutboundOrder`** — order pengiriman dari customer, multi-SKU. Diterima dari sistem eksternal (sales / e-commerce / ERP).

**State & Field**

| State | Field yang dikelola | Keterangan |
|---|---|---|
| **New** | `orderId`, `customerId`, `shipTo`, `orderLines[]` (per SKU: `sku`, `qty`, `uom`) | Order masuk ke WMS. Belum dimasukkan ke Wave. Belum dialokasi stock. |
| **InProgress** | + `waveId` | Sudah dimasukkan ke Wave aktif. Stock allocation sedang/sudah berjalan. |
| **Closed** | _(tidak ada field tambahan)_ | Wave sudah dispatch. Order selesai dari sisi WMS. |

**`Wave`** — grouping OutboundOrder yang diproses bersama-sama untuk efisiensi picking & dispatch.

**State & Field**

| State | Field yang dikelola | Keterangan |
|---|---|---|
| **Active** | `waveId`, `orderIds[]`, `allocations[]` (per line: `sku`, `locationId`, `batch`, `qty`, `stockId`), `pickingTaskIds[]` | SPV buat wave, masukin order. Stock sudah dialokasi via Inventory (`StockAllocated`). PickingTask sudah ter-generate. |
| **Ready** | _(tidak ada field tambahan)_ | Semua PickingTask di `pickingTaskIds[]` completed. Wave siap dispatch. |
| **Dispatched** | _(tidak ada field tambahan)_ | SPV eksekusi dispatch. Truk keluar. Emit `ShipmentDispatched`. Terminal. |

**`PickingTask`** — instruksi mengambil stock dari satu lokasi rak ke staging area.

**State & Field**

| State | Field yang dikelola | Keterangan |
|---|---|---|
| **Assigned** | `taskId`, `waveId`, `stockId`, `sourceLocationId`, `sku`, `batch`, `qty`, `assignedTo` | Generated saat Wave activated, satu PickingTask per entry `allocations[]`. `assignedTo` = operator yang ditugaskan. |
| **Completed** | + `actualQty`, `stagingLocationId` | Operator scan stock + ambil + scan staging destination. Stock pindah ke staging dengan state `Picked`. |

**Catatan:** kalau `actualQty < qty` (rak ternyata kurang), itu **picking discrepancy** — handling belum di-scope. Untuk scope sekarang asumsikan `actualQty = qty`.

#### Flow

**C1 · OutboundOrder masuk;**
Order dari sistem eksternal masuk ke modul Outbound. `orderLines[]` di-snapshot.
`OutboundOrder: (none) → New`

**C2 · SPV input Wave;**
SPV pilih beberapa OutboundOrder yang akan diproses bersama → buat Wave. Tiap order yang masuk: `OutboundOrder: New → InProgress`. Wave langsung emit event `WaveReleased` ke Inventory dengan payload `lines[]` (per-line: orderId, sku, qty).
`Wave: (none) → Active`

**C3 · Inventory alokasi stock;**
Inventory menerima `WaveReleased`. Untuk setiap line, sistem cari Stock dengan state `Available` sesuai allocation strategy (default FEFO — pick batch dengan expiry terdekat). Stock di-mark Allocated ke wave ini. Setelah semua line ter-alokasi, Inventory emit `StockAllocated` dengan detail per-alokasi (sku, locationId, batch, qty, stockId).
`Stock: Available → Allocated`

**C4 · Outbound buat PickingTask;**
Outbound menerima `StockAllocated`. Untuk setiap entry `allocations[]`, sistem create PickingTask dan assign ke operator. `pickingTaskIds[]` di Wave terisi.
`PickingTask: (none) → Assigned`

**C5 · Operator picking;**
Operator scan stock di rak, ambil barang, scan staging location. PickingTask completed. Stock pindah ke staging area dengan state `Picked`.
`PickingTask: Assigned → Completed` · `Stock: Allocated → Picked`

Saat **semua** PickingTask di Wave sudah Completed:
`Wave: Active → Ready`

**C6 · SPV dispatch Wave;**
SPV verifikasi loading ke truck & eksekusi dispatch. Modul Outbound emit event `ShipmentDispatched`. Semua OutboundOrder di wave ditutup. Inventory menerima event dan remove semua Stock dengan state `Picked` yang terikat ke wave ini (stok keluar gudang).
`Wave: Ready → Dispatched` · `OutboundOrder: InProgress → Closed` · `Stock: Picked → (removed)` **selesai**

#### Implikasi & out-of-scope

1. **Picking discrepancy** (rak kurang dari expected, barang rusak saat picking, salah pick) — perlu mekanisme mirip dengan Inbound discrepancy resolution. Belum di-scope.
2. **Wave reschedule / cancel** — kalau Wave dibatalkan saat masih Active, Stock yang sudah Allocated harus di-release kembali ke Available. Belum di-scope.
3. **Customer order source** — di-asumsikan datang dari sistem eksternal. Format integrasi dan validasi order belum di-spec.
4. **Allocation strategy** (FEFO, FIFO, LIFO, fixed-location) di-treat sebagai konfigurasi internal Inventory.

---

## Supporting Modul

### D — Master Data

| Aggregate | Definisi |
|---|---|
| `Warehouse` | Entitas gudang fisik tempat operasional WMS berjalan |
| `Location` | Lokasi spesifik di dalam Warehouse tempat Stock berada |
| `Product` | Master katalog barang/SKU yang dikelola di gudang |

Master Data adalah **referensi statis** yang menjadi sumber kebenaran untuk core modul. Tidak punya state machine kompleks — cukup lifecycle sederhana via flag `isActive`. Semua aggregate di core modul (GR, Stock, OutboundOrder, dll.) merefer ke Master Data via ID.

#### Spesifikasi Aggregate

**`Warehouse`** — entitas gudang fisik.

| Field | Keterangan |
|---|---|
| `warehouseId` (PK) | Identifier unik |
| `name` | Nama gudang (e.g. "DC Jakarta Cakung") |
| `address` | Alamat fisik |
| `isActive` | Soft-delete flag |

GoodsReceipt, Stock, OutboundOrder semua melekat di satu `warehouseId`. Multi-warehouse di-support secara model; koordinasi antar gudang (stock transfer) belum di-scope.

**`Location`** — lokasi spesifik di dalam Warehouse tempat Stock berada secara fisik.

| Field | Keterangan |
|---|---|
| `locationId` (PK) | Identifier unik |
| `warehouseId` (FK) | Lokasi melekat di satu Warehouse |
| `type` | Enum: `ReceivingArea` / `Rack` / `QuarantineArea` / `StagingArea` |
| `code` | Human-readable code (e.g. "REC-01", "RACK-B12-03", "QC-A", "STG-2") |
| `isActive` | Soft-delete flag |

Tipe Location yang dipakai di core flow:
- `ReceivingArea` — tempat Stock baru sehabis `GRConfirmed`, sebelum putaway (state `OnHand`)
- `Rack` — penyimpanan utama (Stock state `Available` dan `Allocated`)
- `QuarantineArea` — isolasi untuk Stock state `Quarantine` (hasil QcHold)
- `StagingArea` — tempat Stock state `Picked`, menunggu dispatch

**Catatan:** hierarki lokasi (Warehouse → Zone → Aisle → Rack → Bin) di-treat sebagai flat dengan `code` yang human-readable di scope ini. Bisa diperluas ke nested hierarchy via tambahan field `parentLocationId` kalau dibutuhkan.

**`Product`** — master katalog barang/SKU.

| Field | Keterangan |
|---|---|
| `sku` (PK) | Stock Keeping Unit, identifier unik |
| `name` | Nama produk |
| `uom` | Unit of Measure default (carton, piece, kg, dll.) |
| `batchTrackingRequired` | Bool — apakah batch wajib di-capture saat scan/allocation |
| `expiryTrackingRequired` | Bool — apakah expiry date wajib di-capture (dan dipakai untuk FEFO) |
| `qcRequiredOnReceipt` | Bool — flag untuk auto-tag `QcHold` saat receiving (lihat catatan di bawah) |
| `shelfLifeDays` | Optional — umur simpan, dipakai untuk validasi expiry saat scan |
| `isActive` | Soft-delete flag |

**Catatan tentang `qcRequiredOnReceipt`:** flag ini disediakan untuk extension — di scope saat ini, `lineStatus` masih di-tag manual oleh operator saat scan. Kalau flag ini di-aktifkan di iterasi berikutnya, sistem otomatis tag line sebagai `QcHold` tanpa intervensi operator.

#### Implikasi ke modul core

1. **Referential integrity**: Aggregate di Inbound/Inventory/Outbound merefer ke Master Data via ID. Sumber kebenaran ada di sini, bukan di dokumen transaksional.
2. **Snapshot vs reference**: Beberapa field critical (`uom`, `batchTrackingRequired`) sebaiknya di-**snapshot** ke aggregate transaksional saat dibuat — misal `expectedLines[]` di GR dan `orderLines[]` di OutboundOrder — supaya perubahan di Product master tidak mengubah dokumen historis. Field non-critical bisa tetap di-reference by ID.
3. **Soft delete only**: `isActive=false`, bukan hard delete. Hard delete bisa break referential integrity dengan dokumen historis (GR lama yang merefer ke SKU yang sudah tidak ada).
4. **Akses via read interface, bukan tabel langsung**: core modul mengakses Master Data lewat **read API gRPC** yang di-expose modul Master Data — **bukan** direct table access. Setiap service punya database sendiri (**DB-per-service**), jadi boundary dijaga oleh abstraction + kontrak gRPC, bukan shared schema. Karena read-only, tidak butuh domain event untuk koordinasi — query **synchronous**, dan di-cache pakai **cache-aside** karena read-heavy.

---

### E — Auth

| Aggregate | Definisi |
|---|---|
| `User` (state: Active, Locked, Disabled) | Identitas yang bisa login ke sistem |
| `Role` | Kumpulan permission yang di-assign ke User |
| `Permission` *(reference entity, non-aggregate)* | Capability granular untuk action tertentu di core modul |
| `RefreshToken` | Token sesi yang dapat dirotasi — siklus issue → rotate → revoke, untuk re-issue access JWT tanpa login ulang |

Auth menangani **authentication** (siapa kamu) + **authorization** (kamu boleh apa).

#### Spesifikasi Aggregate

**`User`** — identitas yang bisa login.

| Field | Keterangan |
|---|---|
| `userId` (PK) | Identifier unik |
| `username` | Unique, untuk login |
| `email` | Untuk reset password + notifikasi |
| `passwordHash` | Hashed credential (detail hashing di §7 Security & Auth) |
| `assignedWarehouseIds[]` | Warehouse-warehouse yang boleh diakses (scoping) |
| `roleIds[]` | Role yang di-assign |
| `failedLoginCount` | Counter untuk lockout policy |
| `isActive` | Soft-delete flag |

**State**

| State | Trigger | Keterangan |
|---|---|---|
| **Active** | Default saat create | User bisa login dan menjalankan action sesuai permission yang dimiliki. |
| **Locked** | `failedLoginCount` melewati threshold | Login ditolak sementara. Auto-unlock setelah cooldown atau admin manual unlock. |
| **Disabled** | Admin disable (`isActive=false`) | Login ditolak permanen sampai admin re-enable. |

**`Role`** — kumpulan permission.

| Field | Keterangan |
|---|---|
| `roleId` (PK) | Identifier unik |
| `code` | Code human-readable (e.g. "SPV", "OPERATOR", "ADMIN") |
| `name` | Display name |
| `permissionIds[]` | Permission yang di-include |
| `isActive` | Soft-delete flag |

Default role yang umum di WMS: `Admin`, `WarehouseManager`, `Supervisor`, `Operator`, `Viewer`.

**`Permission`** — capability granular untuk action. Di-implementasi sebagai reference **Entity** (bukan aggregate root) — reference data yang di-seed, tanpa state machine / domain event / invariant kompleks.

| Field | Keterangan |
|---|---|
| `permissionId` (PK) | Identifier unik |
| `code` | Code action mengikuti pola `Module.Action` |
| `description` | Penjelasan capability |

Permission code yang tumbuh dari core modul (akan dilengkapi seiring fitur bertambah):

| Module | Permission codes (contoh) |
|---|---|
| Inbound | `Inbound.CreateGR`, `Inbound.ScanItem`, `Inbound.PostGR`, `Inbound.HoldGR`, `Inbound.ResolveDiscrepancy` |
| Inventory | `Inventory.CompletePutaway`, `Inventory.AdjustStock` |
| Outbound | `Outbound.CreateWave`, `Outbound.CompletePicking`, `Outbound.DispatchWave` |
| MasterData | `MasterData.ManageProduct`, `MasterData.ManageLocation`, `MasterData.ManageWarehouse` |
| Auth | `Auth.ManageUser`, `Auth.ManageRole`, `Auth.AssignPermission` |

**`RefreshToken`** — catatan persisten refresh token yang diterbitkan saat login; dipakai untuk re-issue access JWT tanpa login ulang. Aggregate root tersendiri (bukan child `User`) karena di-query by hash setiap kali refresh.

| Field | Keterangan |
|---|---|
| `refreshTokenId` (PK) | Identifier unik |
| `userId` (FK) | User pemilik token |
| `tokenHash` | **Hanya hash** yang disimpan — token mentah tidak pernah di-persist (membatasi dampak saat DB kompromi) |
| `issuedAt` / `expiresAt` | Window validitas token |
| `revokedAt` | Optional — timestamp pencabutan (null = belum dicabut) |
| `replacedByTokenId` | Optional — rantai rotasi; saat token tercabut disajikan ulang, seluruh rantai dicabut (defense replay-attack, OWASP refresh-token rotation) |

Tidak punya state enum eksplisit — status aktif dihitung: `IsActive(now) = revokedAt is null && now < expiresAt`. Detail mekanisme (32-byte random, SHA-256 hashed for storage, rotation chain) ada di §7 Security & Auth.

#### Implikasi ke modul core

1. **Authorization check di entry point action** (saat enforcement aktif — lihat "Strategi enforcement" di bawah): setiap command/handler di core modul yang sensitif (post GR, dispatch wave, dll.) di-prefix dengan permission check — biasanya via attribute/decorator (`[Authorize(Permission = "Inbound.PostGR")]`).
2. **Warehouse scoping**: untuk environment multi-warehouse, check permission saja tidak cukup — perlu pastikan user punya akses ke `warehouseId` yang sedang dioperasikan. Biasanya di-enforce di handler atau via global query filter di repository.
3. **Akses via read interface**: sama dengan Master Data — core modul akses User/Role/Permission via service yang di-expose Auth module, bukan direct table.
4. **Tidak ada event ke modul lain**: Auth read-only dari sisi core modul. Saat permission/role user berubah, user mungkin perlu re-login untuk meng-issue token baru (kalau JWT-based dengan claim embedded).

#### Strategi enforcement: deferred selama belum diminta secara eksplisit

Selama belum diminta secara eksplisit, authorization check **tidak di-wire ke command/handler/endpoint**. Permission codes di tabel `Permission` di atas berfungsi sebagai **planning catalog** — mendefinisikan permission yang akan ada saat enforcement diaktifkan, bukan yang sudah aktif di code.

**Implementation guideline:**
- **Jangan pasang** `[Authorize(Permission = "...")]` di command/handler/endpoint saat implementasi fitur
- Authentication (login + JWT) **tetap aktif** → user identity tetap mengalir ke `IAuditable` (untuk capture `createdBy` / `modifiedBy`)
- Pasang comment marker `// TODO-AUTH: <Module.Action>` di lokasi yang nanti akan di-wire — supaya grep manual gampang saat aktivasi
- Terdapat 1 user admin sebagai default.

**Rationale:**
- Mengurangi distraksi saat menulis fitur (fokus business logic, bukan setup role/permission)
- Mempermudah manual & automated testing (tidak perlu setup user/role per skenario)
- Menghindari premature commitment pada permission granularity sebelum fitur stabil

**Trigger aktivasi:**
Dedicated milestone "**Authorization Wire-Up**" sebelum P1 production-ready: grep semua `TODO-AUTH`, apply `[Authorize(Permission = ...)]` dengan code yang sesuai, jalankan authorization test suite.

### F — Reporting

| Read Model | Definisi |
|---|---|
| `StockOnHandView` | Snapshot stok per `(warehouse, sku, batch)` untuk inventory dashboard |
| `ReceivingSummary` | Agregat GR per periode: volume, discrepancy rate, supplier performance |
| `DispatchSummary` | Agregat dispatched wave per periode: volume, throughput |
| `OperatorActivity` | Produktivitas operator: scan count, pick count, putaway count |

Reporting **berbeda struktural** dari core & supporting modul lain: bukan transactional aggregate dengan state machine, tapi kumpulan **read models / projections** yang di-build dari domain event core modul. Tidak menulis ke domain — purely read-side.

#### Pattern: CQRS Read-Side

```
Core modul (write side)  ──domain events──▶  Reporting (read side)
                                                  │
                                                  ▼
                                            Projection tables
                                            (denormalized, query-optimized)
                                                  │
                                                  ▼
                                            Query API ──▶ Dashboard / Report
```

Setiap projection di-update via event handler yang listen ke domain event relevan:

| Event sumber | Projection yang di-update | Operasi |
|---|---|---|
| `GRConfirmed` | `ReceivingSummary` | Tambah qty received, recompute discrepancy rate per supplier |
| `GRConfirmed` (per `receivedLine`) | `StockOnHandView` | Tambah qty per (warehouse, sku, batch) |
| `ShipmentDispatched` | `DispatchSummary`, `StockOnHandView` | Tambah throughput; kurang qty di stock-on-hand |
| `PutawayTask: Completed` | `OperatorActivity` | Increment putaway count untuk operator yang ditugaskan |
| `PickingTask: Completed` | `OperatorActivity` | Increment pick count untuk operator |

#### Karakteristik

| Aspek | Penjelasan |
|---|---|
| **Storage** | Relational (PostgreSQL denormalized table atau materialized view) di P1 cukup. Bisa migrasi ke NoSQL document store kalau volume/query pattern menuntut. |
| **Konsistensi** | Eventual consistency dengan core modul lewat message bus (Azure Service Bus / GCP Pub/Sub) + Outbox. Lag umumnya sub-second; bisa naik saat beban tinggi atau ada retry/DLQ. |
| **Rebuild-able** | Karena di-build dari event, projection bisa di-rebuild ulang dari event store. Implies outbox/event store di-retain cukup lama. |
| **Tidak punya domain invariant** | Projection murni cermin event — validation ada di core modul yang emit event. |
| **Schema dioptimize per use-case** | Beda dengan transactional table yang harus normalize, projection bebas denormalize sesuai query yang di-serve. |

#### Daftar report dasar

| Report | Sumber projection | Use case |
|---|---|---|
| Stock-on-Hand per SKU | `StockOnHandView` | Inventory dashboard, replenishment planning |
| Receiving Aging | `ReceivingSummary` | Berapa GR pending approval > X jam |
| Supplier Performance | `ReceivingSummary` | Discrepancy rate per supplier per periode |
| Dispatch Summary | `DispatchSummary` | Berapa wave dispatched per hari, total volume |
| Operator Productivity | `OperatorActivity` | Throughput per operator untuk KPI |

#### Implikasi ke modul lain

1. **Reporting subscribe ke domain event lintas modul** — satu-satunya supporting modul yang punya dependency event-based ke core. Setiap event payload yang dipakai untuk pelaporan harus stabil dan cukup informatif.
2. **Rebuild capability** — pertimbangkan retain event payload (di outbox atau event store dedicated) cukup lama supaya projection bisa di-replay saat ada schema change atau bug.
3. **Read-only ke core** — Reporting tidak emit event balik ke core modul.

### G — Notifications

| Aggregate | Definisi |
|---|---|
| `NotificationSubscription` | Aturan: user/role mana dapat notifikasi apa, lewat channel apa |
| `NotificationDelivery` (state: Pending, Sent, Failed, Read) | Satu attempt pengiriman notifikasi ke satu user |

Notifications menerjemahkan **domain event** menjadi **pesan ke user** lewat channel (in-app, email, push). Mirip Reporting, ini consumer event — bukan emitter. Beda dari Reporting yang fokus ke read model untuk query, Notifications fokus ke push message ke user secara timely.

#### Trigger yang relevan dari core modul

| Trigger | Notifikasi target | Channel default |
|---|---|---|
| `GoodsReceipt: InProgress → Pending` | SPV warehouse terkait | In-app + push |
| `PutawayTask: Assigned` | Operator yang di-assign | In-app + push |
| `PickingTask: Assigned` | Operator yang di-assign | In-app + push |
| `Wave: Active → Ready` | SPV warehouse terkait | In-app + push |
| Discrepancy `OverDelivery` di-detect | Purchasing role + SPV | In-app + email |
| Stock `Quarantine` melewati threshold umur | QC team + SPV | Email |

Catatan: tabel di atas adalah **default policy** — mapping aktual di-customize via `NotificationSubscription` per user / role.

#### Spesifikasi Aggregate

**`NotificationSubscription`** — aturan kapan & gimana user dapat notifikasi.

| Field | Keterangan |
|---|---|
| `subscriptionId` (PK) | Identifier unik |
| `subscriberType` | Enum: `User` / `Role` |
| `subscriberId` | userId atau roleId |
| `eventType` | Domain event yang di-subscribe (e.g. `"GoodsReceipt.PendingApproval"`) |
| `channels[]` | List channel: `InApp` / `Email` / `Push` |
| `warehouseScope` | Optional `warehouseId` filter (untuk multi-warehouse user) |
| `isActive` | Soft-delete flag |

**`NotificationDelivery`** — satu attempt kirim notifikasi.

**State & Field**

| State | Field yang dikelola | Keterangan |
|---|---|---|
| **Pending** | `deliveryId`, `subscriptionId`, `userId`, `channel`, `payload`, `eventRef` | Notifikasi sudah di-queue, belum dikirim. Worker akan ambil dan dispatch. |
| **Sent** | + `providerMessageId?` | Berhasil dikirim ke channel provider (SMTP, FCM, dll). Belum tentu sudah dibaca user. |
| **Failed** | + `failureReason`, `retryCount` | Gagal kirim. Bisa di-retry sesuai policy retry; setelah max retry → DLQ. |
| **Read** | _(tidak ada field tambahan)_ | User sudah baca notifikasi. Hanya applicable untuk channel `InApp`; tidak ter-track untuk Email/Push. |

#### Karakteristik

| Aspek | Penjelasan |
|---|---|
| **Subscriber dari domain event** | Notifications listen ke event yang sama dengan Reporting  — tapi handler-nya beda: bukan update projection, tapi enqueue `NotificationDelivery`. |
| **Delivery asynchronous** | Setelah `NotificationDelivery: Pending`, worker ambil dan kirim ke channel provider. Tidak in-line dengan event handler supaya tidak block flow utama. |
| **Idempotency** | Subscription bisa trigger ulang untuk event yang sama (e.g. retry, replay). Worker harus idempotent — cek apakah delivery sudah `Sent` sebelum kirim ulang. |
| **Channel provider abstraction** | `IEmailSender`, `IPushNotifier`, `IInAppNotifier` di-abstract via interface; implementasi konkret di-swap (SMTP/SendGrid untuk email, FCM/APNs untuk push). |

#### Implikasi ke modul lain

1. **Read-only ke core modul** — sama dengan Reporting, hanya consume event, tidak emit balik.
2. **Dependency ke Auth & Master Data** — butuh info user (recipient detail, channel preference) dari Auth, dan warehouse dari Master Data untuk scoping. Akses lewat read interface masing-masing.
3. **Channel provider sebagai external dependency** — kegagalan kirim (SMTP down, FCM rate limit) di-isolate sebagai retry + DLQ; tidak boleh propagate ke core modul.