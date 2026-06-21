# Phase 05a — Azure Foundation: Platform.Azure Adapters + Bicep IaC + OIDC

**Status:** planned

**Pre-conditions:**
- **04e done:** sistem jalan penuh lokal via Aspire (semua port punya adapter `Platform.Local`; WebUI + Gateway YARP lokal wiring REST). 6 FF + FF #7–#11 hijau.
- Pembuka **Phase 05 Azure / Mixed PaaS** (prinsip 4: cloud *setelah* lokal benar-benar jalan). Disarankan kerjakan **sebelum** Phase 06 (AZ-204 retire 31 Jul 2026). Punya: Azure subscription + `az` CLI; GitHub repo untuk OIDC.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0018-compute-hosting-mixed-paas.md` (THE Azure compute mapping — port→service)
- `docs/adr/0002-tri-cloud-hexagonal.md` (SDK cloud HANYA di `Platform.<Cloud>` + Hosts; core nol SDK)
- `docs/adr/0024-cross-broker-trace-context-propagation.md` (W3C `traceparent`/`tracestate` → Service Bus application properties)
- `docs/adr/0021-service-to-service-auth.md` (`IServiceTokenProvider` → Managed Identity, offline-validate) · `docs/adr/0016-refresh-token-rotation.md` (signing key di balik `ISecretProvider` → Key Vault) · `docs/adr/0010-data-ownership-db-per-service.md` (schema per service)

**Tujuan:** Realisasikan kolom adapter Azure — tiap port BuildingBlocks dapat implementasi `Platform.Azure` + Host Azure tipis + Bicep shared infra + pipeline OIDC — sehingga ganti cloud = ganti isi adapter, core tak goyang. Satu service naik ke ACA dengan health hijau via OIDC.

**Deliverable:**
- `src/Platform/Wms.Platform.Azure` — adapter per port: `IMessagePublisher`→Azure Service Bus, `ISecretProvider`→Key Vault, `ICacheStore`→Azure Cache for Redis (StackExchange.Redis), `IObjectStore`→Blob Storage, `IServiceTokenProvider`→Managed Identity (`DefaultAzureCredential`), `IDeadLetterStore`→Service Bus DLQ + tabel forensik; map W3C trace-context ⇄ Service Bus application properties (ADR-0024).
- `src/Hosts/Azure/Wms.<Module>.Host.Azure` (thin — wiring `AddServiceDefaults` + adapter Azure, nol logic).
- `deploy/azure/*.bicep` — RG, Azure Database for PostgreSQL Flexible, Service Bus namespace, Key Vault, Azure Cache for Redis, Storage account, Azure Container Registry (ACR).
- `.github/workflows/azure-deploy.yml` — federated OIDC (no long-lived secrets): build → push ACR → deploy.

**Tasks:**
1. Buat `Wms.Platform.Azure` (ref hanya `BuildingBlocks.Application`/`.Infrastructure` + Azure SDK); daftar adapter per port di `AddAzurePlatform`.
2. `ServiceBusMessagePublisher` (`IMessagePublisher`) + map envelope `traceparent`/`tracestate` → application properties (publish) sesuai ADR-0024.
3. `KeyVaultSecretProvider` (`ISecretProvider`); `RedisCacheStore` (`ICacheStore`, StackExchange.Redis); `BlobObjectStore` (`IObjectStore`); `ServiceBusDeadLetterStore` (`IDeadLetterStore`).
4. `ManagedIdentityServiceTokenProvider` (`IServiceTokenProvider`) via `DefaultAzureCredential`, audience-scoped (ADR-0021).
5. `Wms.<Module>.Host.Azure` thin untuk satu service (pilih Inbound) — ref `Platform.Azure`, **tak** ref `Platform.Local`.
6. Bicep shared infra `deploy/azure` (RG, PostgreSQL Flexible, Service Bus, Key Vault, Redis, Storage, ACR) + parameter file.
7. GitHub Actions OIDC: federated credential (Entra app), `azure/login@v2`, build+push image ke ACR, deploy satu service ke ACA.
8. Tambah/update FF di `tests/Wms.Architecture.Tests`: **FF #1** (no cloud SDK di Modules/BuildingBlocks) include `Azure.*`; **FF #6** (`Platform.Azure` tak ref Modules); **FF #9** (no `Local*` adapter di `*.Host.Azure`).

**Definition of Done:**
- `dotnet build Wms.Azure.slnf` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF #1/#6/#9 menegakkan isolasi SDK Azure** (SDK hanya di `Platform.Azure`/Hosts; nol `Local*` di cloud host).
- `az deployment group what-if` (bicep shared infra) sukses; satu service deploy ke ACA via pipeline OIDC, `/health` hijau (smoke).

**Learning objective:** Hexagonal cloud-adapter realization; Bicep IaC; GitHub Actions OIDC (federated, zero long-lived secrets); Managed Identity (`DefaultAzureCredential`); SDK Service Bus / Key Vault / Redis / Blob / ACR.

**Handoff notes:** Kolom adapter Azure + IaC + pipeline OIDC terkunci; satu service membuktikan path ACR→ACA→health. **05b** deploy 5 core service ke ACA + KEDA + Service Bus broker + Managed Identity s2s di atas fondasi ini. `Platform.Local` tetap utuh (Aspire lokal tak terganggu).

**Touchpoint cert:** AZ-204 — Service Bus, Key Vault, Managed Identity, Blob Storage, Azure Cache for Redis, ACR, IaC + CI/CD OIDC → X. PCD — *no cert touchpoint* (phase khusus Azure).
