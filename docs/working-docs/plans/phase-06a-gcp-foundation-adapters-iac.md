# Phase 06a — GCP Foundation: Platform Adapters + Terraform IaC + WIF

**Status:** planned

**Pre-conditions:**
- **04e done:** seluruh sistem berjalan LOCAL via Aspire (7 modul + Gateway + WebUI); port-port `BuildingBlocks` final; `Wms.Platform.Local` adapter lengkap; 6 FF hijau.
- Pembuka **Phase 06 GCP** (track paralel ke Phase 05 Azure, sama-sama depend 04e; **disarankan setelah Azure**). 06a = fondasi adapter + IaC; deploy core penuh baru di 06b–06d.
- Terpasang: `gcloud` CLI + Terraform; GCP project + billing aktif.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0002-tri-cloud-hexagonal.md` (core nol cloud SDK; litmus seam) · `docs/adr/0018-compute-hosting-mixed-paas.md` (pemetaan GCP)
- `docs/adr/0021-service-to-service-auth.md` (GCP SA OIDC ID-token, offline-validate) · `docs/adr/0024-cross-broker-trace-context-propagation.md` (Pub/Sub attributes)
- `docs/adr/0010-data-ownership-db-per-service.md` (DB-per-service; Cloud SQL deploy-time)

**Tujuan:** Realisasikan kolom adapter GCP — `Wms.Platform.Gcp` (satu impl per port pakai GCP SDK) + `Hosts/Gcp` thin + `deploy/gcp` Terraform — membuktikan litmus ADR-0002: nambah GCP menyentuh **hanya** adapter + host + IaC, **nol** perubahan Domain/Application.

**Deliverable:**
- `src/Platform/Wms.Platform.Gcp` — adapter per port: `IMessagePublisher`→Pub/Sub, `ISecretProvider`→Secret Manager, `ICacheStore`→Memorystore (Redis; **reuse keluarga adapter StackExchange.Redis** sama seperti Azure), `IObjectStore`→Cloud Storage, `IServiceTokenProvider`→Service Account OIDC ID-token (audience-scoped), `IDeadLetterStore`→Pub/Sub dead-letter topic; map trace-context (`traceparent`/`tracestate`) ↔ **Pub/Sub message attributes** (ADR-0024).
- `src/Hosts/Gcp/Wms.<Module>.Host.Gcp` — host thin per modul (DI wiring `Platform.Gcp`, nol logika bisnis).
- `deploy/gcp` Terraform: Cloud SQL for PostgreSQL, Pub/Sub topics+subscriptions, Secret Manager, Memorystore, Cloud Storage bucket, Artifact Registry.
- GitHub Actions pipeline: OIDC via **Workload Identity Federation** (keyless) → build/push image ke Artifact Registry → deploy.
- `Wms.Gcp.slnf` (semua core/module project + `Platform.Gcp` + `Hosts/Gcp`, **tanpa** `Platform.Azure`/`Platform.Local`).

**Tasks:**
1. Buat `Wms.Platform.Gcp` + `Hosts/Gcp/Wms.<Module>.Host.Gcp`; daftarkan ke `Wms.Gcp.slnf`.
2. Adapter `IMessagePublisher`→Pub/Sub publisher; map `traceparent`/`tracestate` envelope → Pub/Sub attributes saat publish, restore di konsumsi (ADR-0024).
3. Adapter `ISecretProvider`→Secret Manager; `ICacheStore`→Memorystore via StackExchange.Redis (reuse family); `IObjectStore`→Cloud Storage.
4. Adapter `IServiceTokenProvider`→SA OIDC ID-token audience-scoped (ADR-0021); `IDeadLetterStore`→Pub/Sub dead-letter topic.
5. `deploy/gcp` Terraform: Cloud SQL, Pub/Sub topics+subs, Secret Manager, Memorystore, Cloud Storage, Artifact Registry; remote state backend.
6. GitHub Actions OIDC via WIF (provider+SA binding); job build→push Artifact Registry→deploy satu service.
7. `terraform plan` → `apply` shared infra; deploy satu service ke Cloud Run via pipeline; cek health.

**Definition of Done:**
- `dotnet build Wms.Gcp.slnf` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF#1/#6** (cloud SDK hanya di `Platform.Gcp`/Hosts) + **FF#9** (nol `Local*` adapter di GCP host) pass.
- `terraform -chdir=deploy/gcp plan` lalu `apply` shared infra sukses (idempotent: plan kedua nol-diff).
- Satu service deploy ke **Cloud Run** via pipeline WIF; health endpoint hijau.

**Learning objective:** Realisasi cloud-adapter Hexagonal (port sama, impl GCP); Terraform IaC + remote state; GitHub Actions OIDC via Workload Identity Federation (keyless, tanpa SA key JSON); SDK Pub/Sub / Secret Manager / Memorystore / Cloud Storage; mapping W3C trace-context ke Pub/Sub attributes.

**Handoff notes:** Kolom adapter GCP + IaC siap; satu service hidup di Cloud Run via WIF. Litmus ADR-0002 terbukti (Domain/Application tak tersentuh). **06b** ganti InMemory publisher → Pub/Sub broker dan deploy 5 core service ke Cloud Run.

**Touchpoint cert:** PCD — **X** Pub/Sub · **X** Secret Manager · **X** Memorystore · **X** Cloud Storage · **X** SA OIDC · **X** Artifact Registry · **X** Terraform/WIF. AZ-204 — *none* (phase GCP).
