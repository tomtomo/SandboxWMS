# WMS E2E Smoke Template (reusable)

> **Tujuan:** smoke test CEPAT untuk memastikan fungsional WMS tetap OK setelah perubahan kode — bukan suite
> exhaustive (lihat `WMS-E2E-Autonomous-Test.md` untuk yang lengkap). ~6 skenario menutup: fan-out lintas-service,
> **FEFO split** (area paling rawan regresi), konservasi stok, QcHold/quarantine, discrepancy resolution.
> Bukti dari **UI ter-render + snackbar MudBlazor + Aspire logs**. Discovery ID via REST = boleh.

## Setup (Phase 0–1)
1. **Stack up:** `dotnet run --project src/AppHost/Wms.AppHost/Wms.AppHost.csproj --launch-profile https`
   (background; tunggu semua resource Running/Healthy, migrations Finished).
2. **Discover:** WebUI & gateway port dari **Aspire dashboard** (`https://localhost:17161`, token di AppHost stdout) —
   bukan grep stdout (Aspire 13.x tak echo child endpoint). Biasanya WebUI `:58902`, gateway `:58906`.
3. **Seed master data:** `pwsh docs/working-docs/qa/seed-masterdata.ps1`
   → 2 warehouse (WH1, WH2) + 9 lokasi + 4 produk (SUGAR-1KG, BEEF-500G, NAIL-CTN, RICE-25KG). Idempotent.
4. **Login:** route `/` → `admin` / `ChangeMe123!`. Snackbar "Login sukses." → **redirect `/home`** (blank landing).
   ⚠️ Token TTL ~15 mnt, no silent-refresh → **re-login tiap ~12 mnt** sebelum sub-flow panjang. Bila browser
   bawa token basi (restart stack): Logout → form login muncul (tanpa reload) → login.
5. **Catatan UI:** warehouse & SKU kini **dropdown** (tak ada input GUID/SKU manual). Wave = multiselect order.

## Skenario smoke

| # | Skenario | Aksi | Expected (UI) |
|---|---|---|---|
| **SM1** | Inbound happy + **fan-out 3/3** ⏱️async | `/goods-receipts`: pilih Warehouse **WH1**, SKU **SUGAR-1KG** (UOM auto PIECE), Qty 100 → Create GR → buka detail → **Scan Item** (Actual 100, Batch `SUGAR-LOT-A`, Expiry akhir thn depan, status **Good**) → **Selesai Scan** → **Confirm GR** | GR `Confirmed` ("GRConfirmed emitted"); `/inventory/stocks` → SUGAR @ **REC-01** `OnHand` 100; `/inventory/putaway-tasks` → task `Assigned`; `/reporting/receiving-summary` → row recv=100 |
| **SM2** | Putaway | `/inventory/putaway-tasks` → **Complete** (dest pre-fill **RACK-A1**) → Complete Putaway | snackbar "Stock → Available"; `/inventory/stocks` → SUGAR @ **RACK-A1** `Available` 100 |
| **SM3** | Outbound + **FEFO split** 🎯 ⏱️async | `/outbound/orders` → **Buat Order** (Customer bebas, Ship To bebas, line SUGAR-1KG **30**) → Simpan & Buka Detail. `/outbound/waves` → **Buat Wave** → centang order tsb → Buat Wave | `/inventory/stocks` → **SPLIT**: SUGAR `Available` **70** + SUGAR `Allocated` **30** (total 100, **bukan** 100 Allocated); `/outbound/picking-tasks` → task qty **30** |
| **SM4** | Picking + Dispatch + **konservasi** ⏱️async | `/outbound/picking-tasks` → Complete (Staging **STG-01**). `/outbound/waves/{id}` → **Dispatch Wave** | Stock `Picked`@STG-01 lalu pasca-dispatch **30 hilang**, **70 Available tetap** (recv 100 − ship 30 = 70); Wave `Dispatched`, Order `Closed` |
| **SM5** | Edge: **QcHold → Quarantine** | GR baru (WH1, **BEEF-500G** ×20) → Scan status **QcHold** (qty=20) → Selesai Scan → tab Discrepancies → **Resolve** (`SendToQC`) → **Confirm GR** | Confirm dialog **disabled** saat discrepancy unresolved; pasca-resolve enabled; `/inventory/stocks` → BEEF @ **QC-01** `Quarantine` 20, **tanpa** putaway task |
| **SM6** | Edge: **Short/Over delivery** (opsional) | GR (WH1, **NAIL-CTN** expected 10) → Scan Good qty **8** → Selesai Scan → Resolve **AcceptPartial** → Confirm | `/inventory/stocks` → NAIL @ REC-01 `OnHand` **8** (qty aktual diterima, bukan 10). Varian Over: act 15 → RejectExcess → OnHand 10 |

## Pass criteria
- SM1–SM4 hijau = **core flow (inbound→inventory→outbound) + FEFO split + konservasi OK**.
- SM5–SM6 hijau = **discrepancy/QC path OK**.
- Semua hijau ⇒ WMS fungsional sebagaimana seharusnya. Bila ada yang merah: cek Aspire logs service terkait +
  RabbitMQ DLQ; root-cause di kode service, fix, rebuild (stack stop → full restart → re-seed), re-verify, tambah regression test.

## Known-gaps (catat, bukan bug korektness)
Attachment download belum ter-wire; Complete Picking tanpa actual-qty; Receiving Summary tanpa period-filter; authZ
route gating deferred (ADR-0012 → anon deep-link tetap render, bukan bug). GR-create default WH1 = **FIXED** (dropdown).

## Ledger ringkas (isi tiap run)
```
## Smoke run — <UTC date> — commit <sha>
| Skenario | Hasil | Catatan |
| SM1 fan-out 3/3       | ✅/❌ | |
| SM2 putaway           | ✅/❌ | |
| SM3 FEFO split        | ✅/❌ | |
| SM4 dispatch+konservasi | ✅/❌ | |
| SM5 QcHold            | ✅/❌ | |
| SM6 Short/Over        | ✅/❌ | |
Bug ditemukan/diperbaiki: …
```
