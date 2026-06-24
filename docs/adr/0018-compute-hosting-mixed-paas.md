# ADR-0018: Compute Hosting Model — Mixed PaaS (profil-driven)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Keputusan **deploy-time** — `Hosts/<Cloud>` + `Platform.<Cloud>` + `deploy/`; Domain/Application tetap nol cloud SDK

## Context

Pemilihan compute = keputusan **deploy-time** ([ADR-0002](0002-tri-cloud-hexagonal.md)), hidup di Host + Adapter + IaC; ganti compute = ganti Host + Adapter, struktur project tak goyang. Pertanyaannya: satu model compute **seragam**, atau **pilih per service**? *PaaS* = kita kelola kode + config, platform kelola OS/patching/scaling. Tujuan sandbox (#2 AZ-204, #3 GCP PCD) menuntut **breadth** layanan compute yang diuji (container, web PaaS, serverless, API management) — bukan hanya satu.

## Decision

- **Pilihan:** **Mixed PaaS, profil-driven** — compute tiap service didorong **profil service**; keberagamannya dipilih sadar demi **cert breadth**. Sengaja **tak** turun ke IaaS (VM sendiri) maupun naik ke **Kubernetes mentah**.

**Decision rule (profil → compute):**

| Profil service | Compute model | Alasan |
|---|---|---|
| Stateless: event-driven dan/atau expose gRPC internal (termasuk Auth authority) | **Container** (scale-out, bisa scale-to-zero) | gRPC = HTTP/2 long-lived; consumer message bus butuh proses hidup; authority stateless tak butuh sticky-session |
| Web **stateful** (UI: Blazor SignalR circuit / session) | **Web PaaS always-on** | latensi stabil + sticky session; scale-to-zero merusak Blazor circuit |
| Pure event consumer (no inbound HTTP) | **Functions / serverless trigger** | bayar-per-eksekusi; trigger langsung dari message bus |

**Pemetaan konkret — Azure:**

| Service | Compute | Catatan |
|---|---|---|
| Inbound, Inventory, Outbound, MasterData, Auth | **Container Apps (ACA)** | gRPC internal + event consumer; scale KEDA. Auth = authority stateless + gRPC read-API (profil = MasterData) |
| EdgeGateway | **API Management (managed, via IaC)** | routing + auth/rate-limit untuk REST dari UI; service expose REST sendiri → tak transcoding. Cert AZ-204 (APIM) |
| WebUI | **App Service** (always-on) | Blazor Server SignalR stateful → always-on + session affinity. *Satu-satunya tenant App Service* |
| Reporting, Notification | **Functions** (isolated worker) | message-bus-triggered projection / dispatcher |

**Pemetaan konkret — GCP:**

| Service | Compute | Catatan |
|---|---|---|
| Inbound, Inventory, Outbound, MasterData, Auth | **Cloud Run** (service) | container HTTP/2; padanan terdekat ACA |
| EdgeGateway | **API Gateway / Apigee (managed, via IaC)** | routing REST; cert PCD |
| WebUI | **Cloud Run** + `min-instances ≥ 1` + session affinity | Blazor circuit stateful (GCP tak punya App Service 1:1) |
| Reporting | **Cloud Functions gen2** | event-triggered (gen2 = di atas Cloud Run + Eventarc) |
| Notification | **Cloud Run + Pub/Sub push** | push delivery ke endpoint HTTP |

Gateway = **managed** (APIM / API Gateway / Apigee via IaC), **routing + cross-cutting saja, tanpa transcoding** ([ADR-0006](0006-grpc-internal-rest-ui.md)); YARP hanya lokal. Tiap service expose REST langsung dari `.Api`. **Cert coverage:** AZ-204 → ACA + App Service + Functions + APIM; PCD → Cloud Run + Cloud Functions gen2 + Pub/Sub push + API Gateway/Apigee — persis matriks "develop/deploy compute + API management" yang diuji.

- **Kenapa:** Tiap service di compute yang pas **selaras** tujuan #1 (latihan arsitektur); heterogenitasnya **sengaja** memaksimalkan cert coverage (#2/#3). Boundary bersih ([ADR-0002](0002-tri-cloud-hexagonal.md)) bikin reversibel murah. `→ Canon: Newman (Building Microservices), deployment & "consistency vs heterogeneity"; MS Learn AZ-204 (ACA / App Service / Functions / APIM); Google Cloud PCD (Cloud Run / Functions gen2 / Pub/Sub / API Gateway); ref: dotnet/eShop host model`.
- **Trade-off (deviasi sadar):** Best-practice **produk** condong ke **konsistensi compute** (tekan cognitive load, seragamkan CI/CD & observability); heterogenitas di produksi biasanya *accidental complexity*. Di sini kalkulus terbalik demi tujuan belajar — **bukan disamarkan sebagai "the standard"**. Kalau target = deliver produk, Option B lebih bijak.
- **Kapan ditinjau ulang:** lihat **Revisit** di Out of scope.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Mixed PaaS, profil-driven** *(dipilih)* | Cert breadth maksimal; tiap service di compute pas; reversibel murah berkat boundary bersih | Heterogenitas = cognitive load & CI/CD beragam (deviasi sadar dari best-practice produk) | Newman (Building Microservices) |
| B. Uniform container (ACA / Cloud Run only) *(REJECTED)* | Konsistensi ops; *paling sehat untuk produk nyata* | Kehilangan breadth **App Service** (deployment slots, ARR affinity, plan) & **Functions** (trigger/binding, per-exec billing) yang **diuji** | MS Learn AZ-204 |
| C. Uniform Kubernetes (AKS / GKE) *(REJECTED)* | Kontrol penuh atas orkestrasi | Salah cert (K8s mendalam = AZ-305 / CKA, bukan AZ-204); ops meledak untuk solo; membuang abstraksi serverless-container (ACA = "K8s yang disembunyikan") yang justru diuji | Newman; MS Learn |

## Consequences

**Positif**
- Cert coverage persis matriks "develop/deploy compute + API management" AZ-204 & PCD.
- Profil service memetakan langsung ke ADR lain: pure consumer ([ADR-0017](0017-eventual-consistency-reporting-notification.md)) → serverless; Auth authority ([ADR-0011](0011-master-data-read-api-cache-aside.md)) → container; WebUI stateful → App Service/Cloud Run always-on.
- Yang bikin aman: (1) boundary bersih → reversibel murah; (2) decision rule eksplisit → heterogenitas *intentional*, bukan zoo acak; (3) per-cloud padanan terdekat → simetri yang menjelaskan.

**Trade-off / lebih sulit**
- Tiga model compute = tiga pola CI/CD, observability, & tuning yang harus dikuasai solo.
- **Asimetri Azure↔GCP (di-flag sadar):** Azure menonjolkan 3 tier compute (ACA / App Service / Functions); GCP mengkonsolidasi mayoritas ke **Cloud Run** — tak ada padanan App Service always-on 1:1. **App Engine** kandidat terdekat tapi **sengaja dihindari**: Google memposisikan Cloud Run sebagai penerusnya dan itu yang diuji PCD (latih yang direkomendasikan, bukan legacy-product). Pelajaran lintas-cloud-nya: **layanan setara tak selalu 1:1.**

**Yang harus dijaga**
- Logika handler tetap **agnostic** di Application/Infrastructure; **Functions trigger = inbound adapter**. Seam hexagonal & 6 fitness function ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)) tetap berlaku — SDK cloud hanya di Host + `Platform.<Cloud>`.

## Out of scope / deferred

Pemilihan layanan konkret per environment hidup di `deploy/` (IaC), bukan di struktur project.

**Revisit (pemicu evaluasi ulang):**
- **App Engine vs Cloud Run** untuk WebUI di GCP (sekarang Cloud Run + `min-instances ≥ 1` + affinity).
- Ops 3-model terlalu berat untuk solo → konsolidasi sebagian ke **Option B** (uniform container) via ADR baru (murah, boundary bersih).
- Target cert geser ke **AZ-305 / PCA** → **Option C** (Kubernetes) jadi ADR tersendiri.
- Cold-start Functions & scale-to-zero di path latency-sensitive (mis. login Auth) → tuning per-env `min-instances` / Flex Consumption (knob deploy-time, bukan keputusan struktur).
- Jam-terbang App Service kedua (slot-swap/ARR di konteks API) → bisa balikin Auth ke App Service via ADR; murah karena boundary bersih.

## Sumber

Microsoft Learn — *AZ-204: Develop Azure compute solutions*; *Azure Container Apps* (+ KEDA); *App Service* (slots, plan, ARR affinity); *Azure Functions* (isolated worker, triggers & bindings); *API Management*. · Google Cloud — *Cloud Run*; *Cloud Functions 2nd gen*; *Pub/Sub push*; *API Gateway / Apigee*; *PCD exam guide*. · Newman, *Building Microservices* 2e — deployment & "consistency vs heterogeneity". · dotnet/eShop — host model (containers + workers).

## Amandemen — WebUI hosting idiom (2026-06-24)

`Wms.WebUI` di-upgrade dari **classic Blazor Server** (`_Host.cshtml`, `AddServerSideBlazor`, `MapBlazorHub`) ke **Blazor Web App + InteractiveServer** (`AddRazorComponents().AddInteractiveServerComponents()`, `MapRazorComponents<App>().AddInteractiveServerRenderMode()`, render mode `InteractiveServerRenderMode(prerender: false)`) — mengeksekusi catatan "revisit" Phase 04e (idiom .NET 8).

**Rationale compute TAK berubah:** tetap **server interactivity** (SignalR circuit, stateful) → tetap butuh compute **always-on** (App Service / Cloud Run min-instances≥1 + session affinity, Phase 05c/06c). Yang berubah hanya **idiom project template**, bukan model hosting/compute. Keputusan ADR-0018 tetap berlaku penuh.

**Token persistence:** in-memory circuit `TokenStore` + `ProtectedLocalStorage` (encrypted) untuk survive reload (bukan HttpOnly cookie — menghindari static-form login + plumbing auth-middleware yang menyenggol Phase 07a). Revisit ke HttpOnly cookie + full auth saat **Phase 07a** (authZ wire-up). Ref: `docs/working-docs/plans/webui-maturity-design.md` §3.2.
