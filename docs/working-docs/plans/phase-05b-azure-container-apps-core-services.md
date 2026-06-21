# Phase 05b — Core Services → Azure Container Apps + KEDA + Service Bus

**Status:** planned

**Pre-conditions:**
- **05a done:** `Platform.Azure` adapter per port + Hosts Azure tipis + Bicep shared infra + pipeline OIDC; satu service sudah hijau di ACA. FF #1/#6/#9 menegakkan isolasi SDK Azure.
- Lanjutan **Phase 05** (prinsip 4). Shared infra (Service Bus, Key Vault, Redis, PostgreSQL Flexible, ACR) sudah ter-provision.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0018-compute-hosting-mixed-paas.md` (Inbound/Inventory/Outbound/MasterData/Auth → ACA; scale KEDA; Auth = authority stateless + gRPC read-API)
- `docs/adr/0021-service-to-service-auth.md` (Managed Identity s2s, audience-scoped, **offline-validate** — tak ada RPC ke Auth per-request)
- `docs/adr/0024-cross-broker-trace-context-propagation.md` (consumer restart Activity dari Service Bus application properties)
- `docs/adr/0010-data-ownership-db-per-service.md` (schema per service di PostgreSQL Flexible) · `docs/adr/0002-tri-cloud-hexagonal.md`

**Tujuan:** Naikkan 5 core service ke Azure Container Apps dengan Service Bus sebagai broker nyata (ganti InMemory publisher), KEDA event-driven scaling, dan Managed Identity s2s token tervalidasi offline — full core event chain berjalan di Azure.

**Deliverable:**
- Deploy Inbound, Inventory, Outbound, MasterData, Auth → Azure Container Apps (Bicep `containerApp` + ACA environment, image dari ACR via OIDC).
- KEDA scale rules: Service Bus queue length (consumer) + HTTP concurrency (gRPC/REST), scale-to-zero di service yang aman.
- Service Bus sebagai broker — `ServiceBusMessagePublisher` aktif (ganti InMemory); consumer restart Activity (ADR-0024).
- PostgreSQL Flexible per service (schema `<module>` + `infrastructure`); `MigrationRunner` apply ke tiap service DB.
- Managed Identity s2s antar gRPC internal (ganti Local trust stub), validasi offline (ADR-0021); internal gRPC over ACA.

**Tasks:**
1. Bicep `containerApp` × 5 (Inbound/Inventory/Outbound/MasterData/Auth) + ACA environment + ingress (internal gRPC, external REST per service).
2. KEDA scaler: Service Bus queue-length untuk consumer; HTTP concurrency untuk service inbound; set `minReplicas:0` di service aman (scale-to-zero).
3. Set Service Bus sebagai broker aktif (config-driven publisher Azure) — pensiun InMemory di host Azure; verifikasi consumer restart Activity dari application properties.
4. Provision PostgreSQL Flexible per service via Bicep; jalankan `MigrationRunner` (connection string di-inject) ke 5 DB.
5. Wire Managed Identity s2s: client interceptor mint token audience-scoped per callee; server `[Authorize]` audience policy, `Authority`=Entra issuer, `ValidateAudience=true`, validasi **offline** (ADR-0021).
6. Pasang assignment RBAC Managed Identity (Service Bus Data Owner/Sender-Receiver, Key Vault Secrets User) via Bicep.
7. E2E core chain di Azure: GR create+confirm → `GRConfirmed` → … → `ShipmentDispatched`; amati KEDA scale event.
8. Update FF: **FF #9** scan kelima `*.Host.Azure` (nol `Local*` adapter, termasuk `Local*TokenProvider`); FF #1/#6 tetap hijau.

**Definition of Done:**
- `dotnet build Wms.Azure.slnf` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF #1/#6/#9** menegakkan kelima host Azure bebas `Local*` (s2s pakai Managed Identity, bukan trust stub).
- 5 service healthy di ACA; **full core event chain jalan di Azure** (GR→…→`ShipmentDispatched`); KEDA scale teramati (queue-length / concurrency); s2s Managed Identity token tervalidasi offline (smoke + log).

**Learning objective:** Azure Container Apps (environment, ingress, revision); KEDA event-driven scaling + scale-to-zero implications; Managed Identity s2s (audience-scoped, offline-validate); Service Bus sebagai broker produksi.

**Handoff notes:** 5 core service hidup di ACA via Service Bus + KEDA + Managed Identity s2s. **05c** menambah APIM gateway + App Service WebUI di depan service ini; **05d** menambah Reporting/Notification → Functions. Edge gateway & UI belum naik di sini.

**Touchpoint cert:** AZ-204 — Azure Container Apps, KEDA scaling, Managed Identity, Service Bus → X. PCD — *no cert touchpoint* (phase khusus Azure).

**Out-of-scope:** cold-start tuning di path latency-sensitive login Auth (deferred Phase 07c). QC release, return-to-vendor (out-of-scope global).
