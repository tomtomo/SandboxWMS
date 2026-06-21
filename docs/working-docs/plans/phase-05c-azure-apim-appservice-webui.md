# Phase 05c — APIM Gateway + App Service WebUI (always-on)

**Status:** planned

**Pre-conditions:**
- **05b done:** 5 core service healthy di ACA via Service Bus + KEDA + Managed Identity s2s; full core event chain jalan di Azure.
- Lanjutan **Phase 05** (prinsip 4). Service sudah expose REST sendiri dari `.Api` (tak butuh transcoding di gateway).

**Context refs (WAJIB baca dulu):**
- `docs/adr/0018-compute-hosting-mixed-paas.md` (EdgeGateway → APIM managed via IaC, routing + auth/rate-limit, **NO transcoding**; WebUI → App Service always-on + session affinity; Blazor circuit stateful)
- `docs/adr/0002-tri-cloud-hexagonal.md` (gateway = managed via IaC; YARP hanya lokal)
- `docs/adr/0016-refresh-token-rotation.md` (user JWT RS256 verify offline di edge/service) · `docs/adr/0021-service-to-service-auth.md`

**Tujuan:** Pasang API Management sebagai edge gateway (routing REST + rate-limit + auth, tanpa transcoding) dan host `Wms.WebUI` (Blazor Server) di App Service always-on dengan session affinity — SignalR circuit stabil (scale-to-zero akan memutus circuit).

**Deliverable:**
- API Management (APIM) via Bicep — API + operations route REST ke ACA service; policy rate-limit + auth (validate user JWT RS256, ADR-0016). NO transcoding (service expose REST sendiri).
- App Service plan (always-on) hosting `Wms.WebUI` Blazor Server; ARR session affinity ON (circuit SignalR stabil lintas request); deploy via pipeline OIDC.
- WebUI dikonfigurasi memanggil service via APIM (bukan langsung ke ACA).

**Tasks:**
1. Bicep APIM: instance + API definition + operations routing ke endpoint REST ACA per service; backend pakai ACA ingress URL.
2. APIM policy: `rate-limit-by-key` + `validate-jwt` (RS256, alg-pinning, iss/aud/exp — ADR-0016) di inbound; **tanpa** transcoding/aggregation.
3. Bicep App Service plan always-on (`alwaysOn:true`) + Web App untuk `Wms.WebUI`; aktifkan ARR affinity (`clientAffinityEnabled:true`).
4. Konfigurasi `Wms.WebUI` base URL → APIM gateway; deploy WebUI ke App Service via GitHub Actions OIDC.
5. Verifikasi SignalR circuit stabil: request berurutan tetap di instance sama (affinity), circuit tak putus.
6. E2E via APIM: aksi UI → APIM → service ACA → event chain; render kembali di WebUI.
7. Update FF: `Wms.WebUI` + Host App Service bebas SDK cloud bocor ke core (**FF #1**); **FF #9** App Service host nol `Local*` adapter.

**Definition of Done:**
- `dotnet build Wms.Azure.slnf` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF #1/#9** menegakkan WebUI/host App Service bebas Local* & core nol SDK.
- APIM fronts service + policy (rate-limit + validate-jwt) aktif; WebUI di App Service **always-on**, SignalR circuit stabil lintas request (affinity); **E2E via APIM→service lewat WebUI** hijau (smoke).

**Learning objective:** API Management (routing, rate-limit, `validate-jwt` policy, backend); App Service (plan, always-on, deployment slots, session affinity/ARR); kenapa Blazor circuit butuh always-on (scale-to-zero memutus SignalR).

**Handoff notes:** Edge (APIM) + UI (App Service always-on) hidup di depan core service ACA. **05d** menambah Reporting/Notification → Azure Functions; setelah 05d sistem penuh operasional di Azure (Mixed PaaS). App Service = satu-satunya tenant always-on (ADR-0018).

**Touchpoint cert:** AZ-204 — API Management, App Service → X. PCD — *no cert touchpoint* (phase khusus Azure).
