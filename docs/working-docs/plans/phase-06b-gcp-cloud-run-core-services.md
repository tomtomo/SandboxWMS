# Phase 06b — GCP Cloud Run: 5 Core Services + Pub/Sub Broker

**Status:** planned

**Pre-conditions:**
- **06a done:** `Wms.Platform.Gcp` adapter lengkap + `Hosts/Gcp` + `deploy/gcp` Terraform shared infra applied; WIF pipeline jalan; satu service sudah hidup di Cloud Run; `Wms.Gcp.slnf` build hijau.
- Lanjutan **Phase 06 GCP** — 06b menaikkan 5 core service ke compute GCP + ganti broker ke Pub/Sub.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0018-compute-hosting-mixed-paas.md` (Inbound/Inventory/Outbound/MasterData/Auth → Cloud Run service, container HTTP/2)
- `docs/adr/0021-service-to-service-auth.md` (Workload Identity s2s: SA OIDC ID-token, offline-validate) · `docs/adr/0024-cross-broker-trace-context-propagation.md` (Pub/Sub attributes)
- `docs/adr/0010-data-ownership-db-per-service.md` (Cloud SQL per service) · `docs/adr/0002-tri-cloud-hexagonal.md`

**Tujuan:** Deploy 5 core service (Inbound, Inventory, Outbound, MasterData, Auth) ke **Cloud Run** dengan Pub/Sub sebagai broker nyata (ganti InMemory) + Cloud SQL per service + Workload Identity s2s — core event chain berjalan penuh di GCP.

**Deliverable:**
- 5 service di **Cloud Run** (service, container, **HTTP/2** untuk gRPC internal, request concurrency dikonfigurasi) via Terraform `deploy/gcp`.
- Pub/Sub broker aktif: `Wms.Platform.Gcp` publisher menggantikan InMemory; consumer idempotent (Inbox dedup tetap).
- **Cloud SQL for PostgreSQL** per service (schema-per-service ADR-0010); `MigrationRunner` apply migration ke tiap DB.
- Workload Identity s2s: caller mint SA OIDC ID-token audience-scoped, callee `[Authorize]` audience policy + validasi **offline** (ADR-0021).

**Tasks:**
1. Terraform: definisikan 5 Cloud Run service (image Artifact Registry, HTTP/2 enabled, concurrency, env/secret refs ke Secret Manager).
2. Wire Pub/Sub publisher (`Platform.Gcp`) sebagai `IMessagePublisher` di semua GCP host; pensiunkan InMemory di profil GCP.
3. Provision Cloud SQL per service via Terraform; jalankan `MigrationRunner` (connection string di-inject) ke tiap DB.
4. Konfigurasi SA per service + binding Workload Identity; client interceptor sisipkan SA OIDC ID-token, server validasi audience offline (ADR-0021).
5. Map trace-context producer→consumer via Pub/Sub attributes pada jalur Pub/Sub (ADR-0024).
6. Deploy 5 service via pipeline WIF; verifikasi health + full core event chain (GR→`GRConfirmed`→Stock; Wave→pick→dispatch).

**Definition of Done:**
- `dotnet build Wms.Gcp.slnf` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF#1/#6/#9** (SDK GCP terisolasi `Platform.Gcp`/Hosts; nol `Local*` di GCP host) pass.
- 5 service **healthy** di Cloud Run; **full core event chain** sukses end-to-end di GCP via Pub/Sub.
- Consumer **idempotent** (duplicate Pub/Sub delivery → efek sekali); s2s token WIF tervalidasi **offline** (callee tolak audience salah).
- Smoke: deploy via pipeline WIF → POST create+confirm GR → state Inventory terbentuk lewat Pub/Sub.

**Learning objective:** Cloud Run (service, HTTP/2 untuk gRPC, request concurrency, scale-to-zero); Pub/Sub sebagai message broker (at-least-once → idempotent consumer); Workload Identity service-to-service (SA OIDC, offline-validate); Cloud SQL for PostgreSQL.

**Handoff notes:** Lima core service hidup di Cloud Run dgn Pub/Sub + Cloud SQL + WIF s2s; event chain core penuh jalan di GCP. **06c** tambah API Gateway/Apigee + WebUI di Cloud Run (min-instances≥1); **06d** tambah Reporting (Cloud Functions gen2) + Notification (Pub/Sub push).

**Out-of-scope:** Cold-start tuning untuk path latency-sensitive (mis. login Auth) — di **Phase 07c** (`min-instances`/warm-up = knob deploy-time).

**Touchpoint cert:** PCD — **X** Cloud Run · **X** Pub/Sub · **X** Workload Identity · **X** Cloud SQL. AZ-204 — *none* (phase GCP).
