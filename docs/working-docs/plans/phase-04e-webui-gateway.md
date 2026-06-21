# Phase 04e — WebUI (Blazor Server) + Gateway (YARP local)

**Status:** planned

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

**Handoff notes:** **LOCAL SYSTEM COMPLETE** — 7 service + WebUI + gateway jalan via Aspire, full core+supporting flow E2E lewat UI. Gate prinsip 4 (cloud setelah local works) **satisfied** → buka Phase 05 (Azure) & 06 (GCP), keduanya depend 04e.

**Touchpoint cert:** AZ-204 — App Service rationale (Blazor circuit always-on; branded 05c) + APIM (gateway; 05c) — pattern lokal. PCD — Cloud Run web min-instances≥1 (06c) + API Gateway (06c) — pattern lokal.
