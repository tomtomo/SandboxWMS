# Phase 03a — GoodsReceipt Full State Machine + Two-Axis Discrepancy

**Status:** planned

**Pre-conditions:**
- **02c done:** building block TEMPLATE reusable (pipeline `Result`→transport, AsyncAPI catalog + FF #11, SYSTEM actor + audit log + correlation-id + OTel baseline); `GoodsReceipt` minimal + `GRConfirmedV1` dari 01c ada; FF #1–#11 hijau.
- Pembuka **Phase 03 Complete Core Flow** (prinsip 3): instansiasi template Phase 02 jadi domain penuh — **JANGAN bikin ulang** building block. MasterData belum ada (Phase 04a) → product/uom/location via **LOCAL SEED**, snapshot field kritikal per ADR-0014.

**Context refs (WAJIB baca dulu):**
- `docs/tomsandboxwms-overview.md` §A (GoodsReceipt flow A1–A4, two-axis discrepancy, `GRAttachment`)
- `docs/adr/0013-two-axis-discrepancy-inbound.md` (lineStatus × quantityVariance, dua entry terpisah, invariant resolution-sebelum-Confirm)
- `docs/adr/0015-grattachment-aggregate-object-storage.md` (aggregate terpisah, logical FK, byte di object storage, factory invariant)
- `docs/adr/0014-snapshot-vs-reference-master-data.md` (snapshot kritikal via seed sampai 04a)

**Tujuan:** Naikkan `GoodsReceipt` dari thin (01c) ke **rich aggregate + state machine** penuh — two-axis discrepancy ditegakkan invariant di domain, payload `GRConfirmed` jadi turunan resolusi; tambah aggregate terpisah `GRAttachment` dgn byte off-row lewat port `IObjectStore`.

**Deliverable:**
- `Wms.Inbound.Domain`: `GoodsReceipt` state machine **InProgress→Pending→Confirmed/Hold**; `scannedLines` (`sku`/`actualQty`/`batch`/`expiry`/`lineStatus` Good·WrongItem·QcHold); auto-compute `quantityChecks` (`quantityVariance` Normal·ShortDelivery·OverDelivery); `discrepancies` sebagai **dua sumbu independen** (lineStatus≠Good ∪ variance≠Normal → entry terpisah); `resolutions` (default per type: ShortDelivery→AcceptPartial, OverDelivery→RejectExcess, WrongItem→ReturnToSupplier, QcHold→SendToQC); **invariant: tiap discrepancy punya resolution sebelum Confirm**; `GRConfirmedV1` payload derive `receivedLines` (Good→status, QcHold→status QcHold) / `rejectedLines`.
- Aggregate terpisah `GRAttachment` (root, tabel `inbound.gr_attachments`, logical FK `goodsReceiptId` **tanpa nav property**, factory invariant: contentType whitelist, `sizeBytes`≤50MB, `blobPath` `{grId}/{attachmentId}/{fileName}`).
- `Wms.BuildingBlocks.Application`: port `IObjectStore`. `Wms.Platform.Local`: `LocalObjectStore` (filesystem) adapter.
- Tabel ter-normalize `inbound.gr_expected_lines` / `gr_scanned_lines` / `gr_discrepancies` (FK ke `goods_receipts`).
- `Wms.Inbound.Application` slices: `ScanItem`, `DeclareScanComplete`, `Review`/`ResolveDiscrepancy`, `ConfirmGoodsReceipt` (extend 01c), `HoldGoodsReceipt`, `UploadAttachment`. Marker `// TODO-AUTH` (Inbound.ScanItem / Inbound.ResolveDiscrepancy / Inbound.HoldGR).

**Tasks:**
1. Port `IObjectStore` (`PutAsync`/`GetAsync`/`DeleteAsync` by `blobPath`) di `BuildingBlocks.Application` + `LocalObjectStore` filesystem di `Platform.Local`.
2. `GoodsReceipt` state machine penuh: `ScanItem` (append `scannedLines`, state tetap InProgress), `DeclareScanComplete` (auto-compute `quantityChecks` + compile `discrepancies` dua-sumbu, InProgress→Pending).
3. Model `discrepancies` sebagai dua sumbu independen (ADR-0013) — lineStatus≠Good dan variance≠Normal jadi **entry terpisah**, satu SKU bisa dua entry.
4. `ResolveDiscrepancy` (set `action` per discrepancy, default per type) + `ConfirmGoodsReceipt` extend 01c: enforce invariant **semua discrepancy ter-resolve**, Pending→Confirmed, derive `receivedLines`/`rejectedLines` ke `GRConfirmedV1` via Outbox.
5. `HoldGoodsReceipt` (Pending→Hold, `holdReason`, **tidak** emit event). ⚠ what-happens-after-Hold di luar scope — Hold = terminal di sini.
6. Normalize ke tabel `gr_expected_lines`/`gr_scanned_lines`/`gr_discrepancies` (EF mapping; aggregate tetap satu kesatuan, invariant di domain bukan DB).
7. `GRAttachment` aggregate + factory invariant (contentType whitelist, `sizeBytes`≤50MB, `blobPath` pattern) + repository + tabel `inbound.gr_attachments` (index per `goodsReceiptId`, logical FK tanpa nav).
8. Slice `UploadAttachment`: tulis byte ke `IObjectStore` lalu metadata+`blobPath` ke row (urutan disiplin, cegah orphan); REST endpoint di `Inbound.Api` + marker `// TODO-AUTH`.
9. Domain unit tests: two-axis discrepancy compile (satu SKU Over+QcHold → 2 entry), invariant resolution-required, transisi state legal/ilegal.
10. Integration test: ConfirmGR → `GRConfirmedV1` `receivedLines`/`rejectedLines` benar; upload attachment tulis blob+row; factory reject >50MB / contentType di luar whitelist.

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **semua FF (#1–#11) pass**.
- Domain unit tests hijau: two-axis discrepancy (dua entry), resolution-required invariant tolak Confirm tanpa resolution, state transitions legal/ilegal.
- Integration test hijau: ConfirmGR emit `receivedLines` (Good→status, QcHold→QcHold) / `rejectedLines` benar; `UploadAttachment` tulis blob+row; factory `GRAttachment.Create` tolak >50MB & contentType bad.

**Learning objective:** Rich aggregate + state machine; two-axis discrepancy modeling (ubiquitous language jujur, ADR-0013); enforcement domain invariant di aggregate (bukan DB); aggregate terpisah + reference-by-ID + object storage (byte off-row, ADR-0015).

**Handoff notes:** `GoodsReceipt` full + `GRAttachment` + `IObjectStore` terkunci; `GRConfirmedV1` kini bawa `receivedLines`/`rejectedLines` real (Good vs QcHold). **03b** mengkonsumsi payload kaya ini: QcHold→Stock(Quarantine), Good→Stock(OnHand)+PutawayTask, lalu lengkapi lifecycle Stock + allocation. Snapshot master via seed; diganti read-API di 04a.

**Touchpoint cert:** AZ-204 — Blob Storage *(pattern via `IObjectStore`; Blob konkret di 05)* → X. PCD — Cloud Storage *(pattern; GCS konkret di 06)* → X.

**Out-of-scope:** ⚠ QC release flow (Quarantine→OnHand), return-to-vendor detail (`rejectedLines` cuma metadata event), what-happens-after-Hold — flag sebagai gap, JANGAN dibangun (out-of-scope global).
