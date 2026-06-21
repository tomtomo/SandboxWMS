# Phase 05d — Reporting + Notification → Azure Functions (isolated worker)

**Status:** planned

**Pre-conditions:**
- **05b done:** 5 core service di ACA + Service Bus broker aktif; event chain memancarkan event di Service Bus. (Paralel dengan 05c — sama-sama depend 05b.)
- Lanjutan **Phase 05** (prinsip 4). Reporting/Notification = pure event consumer (no inbound HTTP) → profil serverless (ADR-0018).

**Context refs (WAJIB baca dulu):**
- `docs/adr/0018-compute-hosting-mixed-paas.md` (Reporting/Notification → Functions isolated worker, message-bus-triggered; **Functions trigger = inbound adapter**, handler logic tetap agnostic)
- `docs/adr/0002-tri-cloud-hexagonal.md` (SDK cloud hanya di adapter/host; Application/Infrastructure tak berubah)
- `docs/adr/0024-cross-broker-trace-context-propagation.md` (consumer restart Activity dari Service Bus application properties) · `docs/adr/0010-data-ownership-db-per-service.md` (Reporting projection store sendiri)

**Tujuan:** Naikkan Reporting + Notification ke Azure Functions (isolated worker) dengan Service Bus trigger — Function trigger adalah inbound ADAPTER, handler logic (Application/Infrastructure) tetap agnostic & tak berubah; per-execution billing.

**Deliverable:**
- `src/Hosts/Azure/Wms.Reporting.Host.Functions` + `Wms.Notification.Host.Functions` (.NET 8 isolated worker) — Service Bus trigger sebagai inbound adapter, panggil handler agnostic yang sudah ada.
- Bicep: Function App (isolated worker) × 2 + plan consumption (per-execution) + binding ke Service Bus + storage account untuk Functions runtime.
- Reporting projection update + Notification dispatch ter-trigger dari event Service Bus; restart Activity (ADR-0024).

**Tasks:**
1. `Wms.Reporting.Host.Functions` (isolated worker) — `[ServiceBusTrigger]` map message → handler projection Reporting yang ada (Inbox-committed); trigger = adapter, **tak** ubah Application.
2. `Wms.Notification.Host.Functions` (isolated worker) — `[ServiceBusTrigger]` map → handler dispatch Notification (idempotency + retry/DLQ existing).
3. Restart Activity dari Service Bus application properties di kedua trigger (ADR-0024) → trace end-to-end utuh.
4. Bicep Function App × 2 (isolated worker, consumption plan) + Service Bus binding + storage account runtime + Managed Identity (Key Vault/Service Bus access).
5. Deploy kedua Function via GitHub Actions OIDC (func publish / zip deploy).
6. E2E: event dari core chain → Service Bus → Function fires → Reporting projection ter-update / Notification ter-dispatch; amati per-execution scale.
7. Update FF: **FF #1** core Reporting/Notification (Application/Infrastructure) tetap nol SDK cloud (trigger di host saja); **FF #9** Function host nol `Local*` adapter; **FF #6** `Platform.Azure` tak ref Modules.

**Definition of Done:**
- `dotnet build Wms.Azure.slnf` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF #1/#6/#9** membuktikan handler agnostic (SDK Functions hanya di host; nol `Local*`; core Reporting/Notification tak tersentuh).
- Kedua Function deploy & sehat; Service Bus trigger memicu projection update / notification dispatch; per-execution scale teramati; **E2E reporting + notification di Azure** hijau (smoke).

**Learning objective:** Azure Functions (isolated worker model, triggers & bindings, consumption/per-execution billing); serverless event consumer; Function-as-inbound-adapter (handler tetap agnostic).

**Handoff notes:** **Sistem penuh operasional di Azure** (Mixed PaaS: ACA + APIM + App Service + Functions). **Phase 06** = padanan GCP (Cloud Run + API Gateway + Cloud Functions gen2 + Pub/Sub push). **Phase 07** = cross-cutting deep (authz enforce, observability penuh, resilience calibration, security hardening).

**Touchpoint cert:** AZ-204 — Azure Functions (isolated worker, triggers & bindings) → X. PCD — *no cert touchpoint* (phase khusus Azure).

**Out-of-scope:** allocation-failure / picking-discrepancy / wave reschedule (out-of-scope global); cold-start tuning (Phase 07c).
