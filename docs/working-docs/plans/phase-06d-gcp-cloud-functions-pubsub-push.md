# Phase 06d — GCP Cloud Functions gen2 (Reporting) + Pub/Sub Push (Notification)

**Status:** planned

**Pre-conditions:**
- **06b done:** 5 core service di Cloud Run + Pub/Sub broker + Cloud SQL + WIF s2s; `Wms.Gcp.slnf` build hijau; 6 FF hijau. (06c paralel; 06d hanya depend 06b.)
- Penutup **Phase 06 GCP** — akhir sub-phase ini = **sistem penuh operasional di Azure DAN GCP** → gate prinsip 5.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0018-compute-hosting-mixed-paas.md` (Reporting → Cloud Functions gen2/Eventarc; Notification → Cloud Run + Pub/Sub push; **Functions trigger = inbound adapter**, handler tetap agnostic)
- `docs/adr/0024-cross-broker-trace-context-propagation.md` (Pub/Sub attributes) · `docs/adr/0002-tri-cloud-hexagonal.md`
- `docs/adr/0021-service-to-service-auth.md` (push endpoint auth) · `docs/adr/0010-data-ownership-db-per-service.md` (Reporting projection store)

**Tujuan:** Deploy dua pure event-consumer ke compute serverless GCP — **Reporting → Cloud Functions gen2** (Eventarc-triggered, di atas Cloud Run) dan **Notification → Cloud Run + Pub/Sub push subscription** (delivery ke endpoint HTTP) — logika handler tetap agnostic; trigger = inbound adapter (ADR-0018).

**Deliverable:**
- **Reporting** → **Cloud Functions gen2** via Terraform: di-trigger **Eventarc** dari Pub/Sub; handler projection (Inbox-committed) tak berubah, hanya adapter trigger.
- **Notification** → **Cloud Run + Pub/Sub push subscription**: subscription push delivery ke endpoint HTTP Notification; idempotent + retry/DLQ (Pub/Sub dead-letter, `IDeadLetterStore`).
- Trace-context dibawa di Pub/Sub attributes hingga ke kedua consumer (ADR-0024); push endpoint terlindung (audience/OIDC, ADR-0021).

**Tasks:**
1. Terraform: Reporting sebagai Cloud Functions gen2 + Eventarc trigger dari topic Pub/Sub relevan; bind SA + secret refs.
2. Bungkus handler Reporting di adapter trigger gen2 (handler agnostic tak berubah); commit projection-write + inbox mark satu tx.
3. Terraform: Notification di Cloud Run + Pub/Sub **push** subscription → endpoint HTTP; set dead-letter topic + max delivery attempts.
4. Amankan push endpoint (OIDC token verification / audience, ADR-0021); pastikan handler idempotent.
5. Pastikan trace-context (Pub/Sub attributes) ter-restore di kedua consumer (ADR-0024).
6. E2E GCP: event inti memicu projection Reporting (via Eventarc) + delivery Notification (via push).

**Definition of Done:**
- `dotnet build Wms.Gcp.slnf` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF#1/#6/#9** pass (SDK GCP terisolasi; handler tetap agnostic; nol `Local*` di GCP host).
- Cloud Functions gen2 **terpicu via Eventarc**; Notification **menerima Pub/Sub push** (idempotent; poison → dead-letter).
- Smoke E2E GCP: reporting projection + notification delivery sukses end-to-end (deploy via pipeline WIF).

**Learning objective:** Cloud Functions gen2 (Eventarc trigger, gen2 dibangun di atas Cloud Run); model delivery Pub/Sub **push vs pull**; trigger serverless sebagai inbound adapter Hexagonal (handler agnostic — seam ADR-0018/ADR-0002 terjaga).

**Handoff notes:** **Sistem penuh operasional di BOTH Azure + GCP.** Gate **prinsip 5** terpenuhi → lanjut **Phase 07** cross-cutting deep (authz wire-up `TODO-AUTH`, observability distributed tracing, resilience, security hardening) yang berlaku lintas kedua cloud.

**Touchpoint cert:** PCD — **X** Cloud Functions gen2 (Eventarc) · **X** Pub/Sub push. AZ-204 — *none* (phase GCP).
