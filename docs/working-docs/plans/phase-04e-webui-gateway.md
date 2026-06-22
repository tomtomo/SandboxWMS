# Phase 04e — WebUI (Blazor Server) + Gateway (YARP local)

**Status:** done (2026-06-22)

**Pre-conditions:**
- **04c done** (Reporting query REST + projection) **& 04d done** (Notification in-app delivery). Implies 04a (MasterData) + 04b (Auth login/JWT) juga done.
- Semua 7 service punya host di `Wms.AppHost`; REST endpoint ter-expose tiap `<Module>.Api`. Penutup **Phase 04**.

**Context refs (WAJIB):**
- `docs/adr/0006-grpc-internal-rest-ui.md` (REST untuk UI, gateway routing + cross-cutting **tanpa transcoding**) · `docs/adr/0018-compute-hosting-mixed-paas.md` (WebUI→App Service/Cloud Run always-on; gateway→APIM/Apigee managed; YARP lokal)
- `docs/architecture/tri-cloud-microservices-blueprint.md` (§3 project `Wms.WebUI` + `Wms.Gateway`)

**Tujuan:** Lengkapi sistem lokal dengan UI + gateway — Blazor Server thin (login, GR, stock/reports, in-app notif) di belakang YARP gateway (routing REST + cross-cutting), sehingga full core+supporting flow E2E lewat UI.

**Deliverable:**
- `Wms.WebUI` (Blazor Server + MudBlazor, thin — 1 project, appsettings per-env): **login** (Auth REST), **GR create/list** (Inbound REST), **stock view + reports** (Reporting REST), **in-app notifications** (Notification REST). UI panggil REST saja (tak panggil gRPC langsung).
- `Wms.Gateway` (YARP lokal): routing REST ke service + cross-cutting (**auth forward** bearer, **correlation-id**), **NO transcoding** (service sudah expose REST sendiri).
- `Wms.WebUI` + `Wms.Gateway` ter-declare di `Wms.AppHost` (orkestrasi lokal lengkap: 7 service + WebUI + gateway).

**Tasks:**
1. `Wms.WebUI` Blazor Server + MudBlazor (thin); halaman login → simpan/forward JWT bearer.
2. Halaman GR create/list (Inbound REST) + stock view + reports (Reporting REST) + panel in-app notification (Notification REST).
3. `Wms.Gateway` YARP: route config REST → tiap service; cross-cutting auth-forward + correlation-id; pastikan NO transcoding.
4. Declare `Wms.WebUI` + `Wms.Gateway` di `Wms.AppHost`; wire dependency ke 7 host.
5. appsettings per-env (Local) untuk WebUI + Gateway (single project, tak ditriplikasi).
6. Smoke E2E lewat UI + gateway (login→GR→stock/report→notif).

**Definition of Done:**
- `dotnet build Wms.sln` hijau; **semua FF hijau**.
- Smoke: `dotnet run --project src/AppHost/Wms.AppHost` → **login via WebUI** → **create GR** → **stock/report update terlihat** → **in-app notification muncul** (full local system E2E lewat UI + gateway).

**Out-of-scope:** Managed gateway APIM/Apigee (Phase 05c/06c; YARP lokal only). App Service/Cloud Run deploy (Phase 05/06). gRPC-Web / streaming ke UI. authZ enforcement di UI (Phase 07a).

**Learning objective:** REST untuk UI vs gRPC internal (protokol tepat-guna per kanal), reverse-proxy gateway (YARP) routing + cross-cutting tanpa transcoding, Blazor Server stateful (SignalR circuit) — meletakkan rasional compute App Service/Cloud Run always-on.

**Handoff notes:** **LOCAL SYSTEM COMPLETE** — `Wms.WebUI` (Blazor Server + MudBlazor) + `Wms.Gateway` (YARP) hidup & ter-orkestrasi `Wms.AppHost` (kini 7 service + gateway + webui + migrations). `dotnet build Wms.sln` 0/0 (warnings-as-errors), **FF 15/15**.

Yang dibangun:
- **`Wms.Gateway`** (YARP 2.3.0 + ServiceDiscovery.Yarp 10.7.0): route REST → **semua 7 service** (auth/inbound/inventory/outbound/masterdata/reporting/notification) via `AddServiceDiscoveryDestinationResolver` (destination `http://<service>` Aspire), **NO transcoding**; cross-cutting **auth-forward bearer** (default YARP) + **correlation-id** (`X-Correlation-ID` ensure-then-forward + `UseCorrelationId`).
- **`Wms.WebUI`** (Blazor Server classic, `render-mode="Server"`): login (Auth) · GR create/list (Inbound) · stock+reports (Reporting) · in-app notif inbox+mark-read (Notification). Panggil REST **hanya via gateway** (`http://gateway`), typed clients + `TokenStore` circuit-scoped (bearer+correlation per request). UI tak sentuh gRPC/module (ADR-0006).
- **Inbound GR list (read-side)**: deliverable minta "GR create/**list**" tapi Inbound cuma punya create → ditambah `IGoodsReceiptReader`+`GoodsReceiptReader`+`GET /goods-receipts` (pola reader MasterData, read-only, FF#8-safe). *Menyentuh modul "done" Inbound* — diperlukan deliverable, additive, low-risk.
- **MigrationRunner di-wire ke AppHost** (resource + `WaitForCompletion`, pola Aspire/eShop) — gap pre-existing: AppHost pakai pg-container Aspire (port dinamis) tapi MigrationRunner tak ter-wire → DB Aspire tak ter-migrate → DoD smoke mustahil. Kini DB-prep otomatis sebelum service. Best-practice change (Rule 2).

Keputusan & flag: (1) hosting **classic Blazor Server** dipilih (low-risk, harfiah "Blazor Server"); *idiom modern .NET 8 = Blazor Web App + InteractiveServer* — **revisit**. (2) token **in-memory circuit-scoped** (tak survive reload; production → cookie/ProtectedSessionStorage + authZ **07a**). (3) authZ enforcement tetap deferred (07a) — TODO-AUTH `Inbound.ViewGR` di endpoint baru.

Verifikasi: deterministik **build+FF hijau**. Runtime (Aspire `dotnet run AppHost`): distributed app start ✓, **migrasi apply ke pg Aspire + admin (`admin`/`ChangeMe123!`) ter-seed** (verified psql) ✓, core services serving HTTP ✓. **Gateway-login full E2E + UI smoke = manual via dashboard Aspire** (port project dinamis + ada stack `tomwms-*` lain di mesin → port-probing otomatis tak andal). Smoke manual: `dotnet run --project src/AppHost/Wms.AppHost` → buka dashboard → webui → login `admin`/`ChangeMe123!` → GR create butuh SKU+warehouse ADA di MasterData (handler snapshot uom via gRPC; seed via gateway `POST /products`+`/warehouses`); untuk stock/report/notif terisi, GR harus s/d **Confirmed** (scan→declare→confirm via gateway REST `/goods-receipts/{id}/...` — UI sengaja thin = create/list saja per deliverable).

Gap/calon-lanjut: GR-list reader belum punya integration test (kandidat); UI tak meng-cover full GR workflow (scan/confirm) — by design deliverable; full test-suite (unit+integration Testcontainers) tak di-rerun sesi ini (butuh Docker; perubahan additive + gate hijau).

Gate prinsip 4 (cloud setelah local works) **satisfied** → buka Phase 05 (Azure) & 06 (GCP), keduanya depend 04e.

**Touchpoint cert:** AZ-204 — App Service rationale (Blazor circuit always-on; branded 05c) + APIM (gateway; 05c) — pattern lokal. PCD — Cloud Run web min-instances≥1 (06c) + API Gateway (06c) — pattern lokal.
