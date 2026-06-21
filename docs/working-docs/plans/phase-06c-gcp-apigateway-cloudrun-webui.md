# Phase 06c — GCP API Gateway/Apigee + Cloud Run WebUI

**Status:** planned

**Pre-conditions:**
- **06b done:** 5 core service healthy di Cloud Run via Pub/Sub + Cloud SQL; WIF s2s tervalidasi offline; `Wms.Gcp.slnf` build hijau; 6 FF hijau.
- Lanjutan **Phase 06 GCP** — 06c memasang edge (gateway managed) + WebUI stateful.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0018-compute-hosting-mixed-paas.md` (EdgeGateway → API Gateway/Apigee; WebUI → Cloud Run min-instances≥1 + session affinity; tak ada App Service 1:1)
- `docs/adr/0002-tri-cloud-hexagonal.md` (host/IaC saja yang berubah) · `docs/adr/0021-service-to-service-auth.md` (audience saat gateway→service)
- `docs/adr/0024-cross-broker-trace-context-propagation.md`

**Tujuan:** Pasang **API Gateway / Apigee** managed sebagai edge yang merutekan REST ke service (tanpa transcoding), dan deploy `Wms.WebUI` (Blazor Server) ke **Cloud Run** dengan `min-instances ≥ 1` + session affinity — padanan GCP untuk App Service always-on (circuit SignalR stateful).

**Deliverable:**
- **API Gateway / Apigee** via Terraform `deploy/gcp`: route REST ke 5 service (cross-cutting auth/rate-limit di edge), **no transcoding** (tiap `.Api` expose REST langsung; gRPC tetap internal).
- `Wms.WebUI` Blazor Server → **Cloud Run** dgn `min-instances ≥ 1` + **session affinity** ON (circuit SignalR stateful; **no scale-to-zero**).
- `Hosts/Gcp` wiring WebUI (config gateway base URL); IaC menyetel affinity + min-instances.

**Tasks:**
1. Terraform: definisikan API Gateway/Apigee + API config (routing REST per service, policy auth/rate-limit edge), no transcoding.
2. Deploy `Wms.WebUI` ke Cloud Run: set `min-instances = 1` (≥1) + session affinity ON; arahkan WebUI ke base URL gateway.
3. Verifikasi gateway memfront 5 service (REST reachable lewat edge, bukan langsung).
4. Verifikasi circuit Blazor stabil di Cloud Run (reconnection tidak putus karena scale-to-zero / instance hop).
5. E2E lewat browser: aksi WebUI → gateway → service → balik, melintasi flow inti.

**Definition of Done:**
- `dotnet build Wms.Gcp.slnf` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF#1/#6/#9** pass (penambahan murni host+IaC, nol perubahan Domain/Application; nol `Local*` di GCP host).
- Gateway memfront service (request lewat edge OK; rute langsung sesuai kebijakan); WebUI di Cloud Run `min-instances≥1`, **circuit Blazor stabil**.
- Smoke E2E: gateway → services **melalui** WebUI sukses (deploy via pipeline WIF).

**Learning objective:** API Gateway/Apigee (managed gateway, routing + cross-cutting tanpa transcoding); Cloud Run `min-instances` + session affinity untuk circuit Blazor stateful — **padanan GCP** untuk App Service always-on (pelajaran ADR-0018: layanan setara tak selalu 1:1).

**Handoff notes:** Edge + WebUI hidup di GCP; pengguna mengakses sistem lewat gateway→WebUI→service di Cloud Run. **06d** melengkapi GCP dgn Reporting (Cloud Functions gen2/Eventarc) + Notification (Pub/Sub push) → sistem penuh operasional di GCP.

**Touchpoint cert:** PCD — **X** API Gateway/Apigee · **X** Cloud Run (min-instances, session affinity). AZ-204 — *none* (phase GCP).
