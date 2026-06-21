# Enterprise Tri-Cloud Microservices — Project Structure Blueprint

> **Sifat:** Acuan **struktur project** (abstraksi) — *domain-agnostic* & *service-agnostic*. Reusable lintas-app (WMS, CRP, HRMS, …).
> **Ditentukan di sini:** struktur solution/project. **TIDAK ditentukan di sini:** pemilihan layanan cloud konkret (compute, messaging, secret store, dll). Itu keputusan *deploy-time* per environment / per fokus belajar. Struktur ini **netral** terhadap pilihan tersebut — ganti layanan = ganti **isi adapter**, struktur tetap.

---

## 1. Konteks & tujuan

**Tujuan sandbox:** (1) Enterprise software design & architecture · (2) AZ-204 (Azure) · (3) GCP Professional Cloud Developer.

**Arsitektur (fixed):** Microservices sejak awal · Domain-Driven Design · Clean Architecture · CQRS · Vertical Slice · Event-Driven Architecture.

**Deploy target:** Local · Azure · GCP — desain siap **cloud ke-N** tanpa rombak.

**Yang dijawab dokumen ini:** *struktur project best-practice-nya bagaimana* — sekali matang, dipakai konsisten jangka panjang.

---

## 2. Prinsip struktur (kenapa terbentuk begini)

1. **Core cloud-agnostic; adapter & host per-cloud.** Domain + Application tak kenal SDK apa pun. Tiap cloud punya project **adapter** + **host** sendiri. Isolasi SDK terjadi di **batas project reference** — host yang me-reference adapter satu cloud, build-nya tak menyeret SDK cloud lain. *(Hexagonal / Ports & Adapters; Clean Architecture Dependency Inversion.)*
2. **Modul = bounded context, pemilik datanya sendiri.** Antar-modul **hanya** lewat `*.Contracts` (published language, ber-versi) — tak pernah menyentuh Domain/Infrastructure modul lain. DbContext modul hanya menyentuh schema `<module>`-nya; **tak ada modul membaca store modul lain** (data lintas-context mengalir hanya via event/Contracts). Schema-schema itu satu DB fisik atau DB-per-service = keputusan deploy-time.
3. **Dependency rule + Vertical Slice + CQRS.** Antar-layer: arah dependensi menuju Domain (Clean Architecture). Di dalam modul: Application diorganisir **per slice / use-case** (command-query + handler + validator + endpoint, self-contained), bukan per-folder-teknis. Sisi *command* lewat aggregate; sisi *query* baca langsung ke read-DTO (bypass aggregate/repo) — itu inti CQRS.
4. **Port abstrak, adapter konkret — netral layanan.** Port (`IMessagePublisher`, `ISecretProvider`, …) tak menyebut produk. Adapter per-cloud yang memilih implementasi; *layanan mana* yang dipakai ditentukan saat implement/deploy, **bukan** di struktur.

**Aturan penempatan "shared":** agnostic (no SDK) → `BuildingBlocks` · cloud-coupled (SDK) → `Platform.<Cloud>` · kontrak antar-service → `*.Contracts`/`*.Grpc` · perakit port↔adapter → `Platform.Hosting`.

**Aliran antar-context:** domain event (`IDomainEvent`, `BuildingBlocks.Domain`) bersifat *in-process* di transaksi aggregate, lalu **diterjemahkan** jadi integration event ber-versi (`<Module>.Contracts`) — hanya integration event yang dipersist Outbox & dipublish broker (tipe Domain internal tak pernah jadi wire-contract). Di sisi konsumen, tipe contract asing diterjemahkan ke model sendiri di batas (biasanya di handler integration-event) = **Anti-Corruption Layer**; *Conformist* (pakai contract apa adanya) tetap sah untuk kasus sederhana.

---

## 3. Solution tree

Placeholder: `<App>` (mis. `Wms`) · `<Module>` (bounded context) · `<Cloud>` ∈ {`Local`,`Azure`,`Gcp`,…}.

```
<App>/                                          # repo root (monorepo)
├─ <App>.sln
├─ <App>.Local.slnf · <App>.Azure.slnf · <App>.Gcp.slnf      # filter fokus per-env
├─ Directory.Build.props · Directory.Packages.props · nuget.config · .editorconfig
│
├─ src/
│  ├─ BuildingBlocks/                          # SHARED AGNOSTIC — NOL cloud SDK
│  │  ├─ <App>.BuildingBlocks.Domain           #   seedwork: AggregateRoot, ValueObject, Result<T>, StronglyTypedId, IDomainEvent
│  │  ├─ <App>.BuildingBlocks.Application       #   ICommand/IQuery, PORT abstrak, pipeline behavior (CQRS)
│  │  ├─ <App>.BuildingBlocks.Infrastructure    #   Outbox, Inbox/idempotency, EF interceptor, resilience, telemetry setup
│  │  └─ <App>.BuildingBlocks.Web               #   IEndpoint, ProblemDetails, correlation-id, auth plumbing, gRPC interceptor
│  │
│  ├─ Platform/                                # SHARED CLOUD-COUPLED + composition glue — SDK DI SINI
│  │  ├─ <App>.Platform.Hosting                #   bootstrap agnostic (service defaults: logging, telemetry, health) — NOL cloud SDK
│  │  ├─ <App>.Platform.Local                  #   implementasi port pakai stack lokal
│  │  ├─ <App>.Platform.Azure                  #   implementasi port pakai Azure SDK
│  │  └─ <App>.Platform.Gcp                    #   implementasi port pakai GCP SDK
│  │     # nambah cloud = nambah SATU project <App>.Platform.<Cloud>
│  │
│  ├─ Modules/                                 # tiap modul = bounded context, cloud-agnostic
│  │  └─ <Module>/                             #   CORE → full-5 · SUPPORTING → collapse (1–2 project)
│  │     ├─ <App>.<Module>.Domain              #   aggregate, value object, domain event, invariant
│  │     ├─ <App>.<Module>.Application          #   Features/<UseCase>/{Command,Query,Handler,Validator}  ← vertical slice
│  │     ├─ <App>.<Module>.Infrastructure       #   DbContext (schema "<module>" — milik modul ini), repo, AddXxxModule()
│  │     ├─ <App>.<Module>.Api                  #   gRPC service impl + REST endpoint, MapXxxEndpoints()
│  │     ├─ <App>.<Module>.Contracts            #   integration event (POCO record, ZERO transport dep) — PUBLIK, ber-versi
│  │     └─ <App>.<Module>.Grpc                 #   (opsional) .proto + stub sync — hanya bila modul expose sync gRPC API
│  │
│  ├─ Hosts/                                   # composition root — TIPIS. Di sini SDK isolation terjadi.
│  │  ├─ Local/  <App>.<Module>.Host.Local
│  │  ├─ Azure/  <App>.<Module>.Host.Azure      #   → ref <Module>.Api + .Infrastructure + Platform.Azure + Platform.Hosting
│  │  └─ Gcp/    <App>.<Module>.Host.Gcp
│  │
│  ├─ Gateway/  <App>.Gateway                  # reverse-proxy agnostic (lokal) · cloud = managed gateway via IaC
│  ├─ WebUI/    <App>.WebUI                    # UI client — 1 project, appsettings per-env (tak ditriplikasi)
│  └─ AppHost/  <App>.AppHost                  # orkestrasi LOKAL (mis. .NET Aspire): semua host + dependency
│
├─ deploy/                                     # IaC — DI SINILAH pemilihan layanan cloud hidup, bukan di src/
│  ├─ local/   (wiring di AppHost)
│  ├─ azure/   IaC Azure (mis. Bicep)
│  └─ gcp/     IaC GCP (mis. Terraform)
│
└─ tests/
   ├─ <App>.<Module>.Domain.UnitTests
   ├─ <App>.<Module>.IntegrationTests          # Testcontainers (real DB/broker)
   ├─ <App>.Architecture.Tests                 # NetArchTest — fitness functions (§4)
   └─ <App>.Contracts.Tests                    # consumer-driven contract (mis. Pact)
```

**Right-sizing (per tipe subdomain):** *core* (domain kaya) → full-5 · *supporting* CRUD → 1 project + `.Contracts` · *pure consumer* (read-model/notifier) → 1 project · *generic* (problem terpecahkan: auth, notifikasi, dsb) → **adopt off-the-shelf**, integrasi via adapter/ACL — bukan modul penuh (kecuali sengaja dibangun untuk tujuan belajar). Jangan paksa 5 project ke modul CRUD.

---

## 4. Dependency rule & fitness functions

**Arah referensi — peta otoritatif yang mengunci struktur:**
```
# Kernel (BuildingBlocks) — internal, ikut aturan menuju Domain
BuildingBlocks.Application ──▶ BuildingBlocks.Domain
BuildingBlocks.Infrastructure ──▶ BuildingBlocks.Application
BuildingBlocks.Web ──▶ BuildingBlocks.Application

# Modul — tiap layer menuju Domain & menuju kernel se-layer
<Module>.Domain ──▶ BuildingBlocks.Domain
<Module>.Application ──▶ <Module>.Domain, BuildingBlocks.Application
<Module>.Infrastructure ──▶ <Module>.Application, BuildingBlocks.Infrastructure   (+ EF/lib agnostic, TANPA cloud SDK)
<Module>.Api ──▶ <Module>.Application, <Module>.Contracts, BuildingBlocks.Web
<Module>.Contracts ──▶ (nothing)               # POCO event record, dependency-free
<Module>.Grpc ──▶ (Grpc/Protobuf)              # .proto + stub sync, terpisah dari event

# Platform & host (satu-satunya pemegang cloud SDK)
Platform.<Cloud> ──▶ BuildingBlocks.Application (ports) + cloud SDK
Platform.Hosting ──▶ BuildingBlocks.Infrastructure, BuildingBlocks.Web   # service-defaults agnostic, NOL cloud SDK
Host.<Cloud> ──▶ <Module>.Api, <Module>.Infrastructure, Platform.<Cloud>, Platform.Hosting

# Antar-modul — hanya kontrak publik
<Module-A> ──▶ <Module-B>.Contracts (event) / <Module-B>.Grpc (sync)   — tak pernah Domain/Application/Infrastructure
```

**Enam fitness function (NetArchTest — fail build kalau dilanggar):**
1. Tak ada SDK cloud (`Azure.*`/`Google.*`/`Amazon.*`) di `Modules.*` & `BuildingBlocks.*` — SDK hanya di `Platform.<Cloud>` + Hosts.
2. `*.Domain` nol framework (no EF / no mediator / no ASP.NET).
3. Modul tak me-reference Domain/Application/Infrastructure modul lain — hanya `*.Contracts` / `*.Grpc`.
4. `BuildingBlocks` tak me-reference `Modules`/`Platform` (kernel tak kenal konsumennya).
5. **Dependency rule intra-modul:** `*.Domain` tak me-reference `*.Application`/`*.Infrastructure`/`*.Api`; `*.Application` tak me-reference `*.Infrastructure`/`*.Api` — dependensi mengalir hanya menuju Domain.
6. `Platform.*` tak me-reference `Modules.*` — adapter cuma implement port abstrak (BuildingBlocks) + cloud SDK, tak kenal fitur konkret modul.

*Konvensi yang tak di-test akan luntur — enam aturan ini yang menjaga blueprint tetap utuh seiring waktu.*

---

## 5. Alur pertumbuhan

| Nambah… | Bikin (tempat sudah jelas) | JANGAN sentuh |
|---|---|---|
| **Modul** | `Modules/<M>/{Domain,App,Infra,Api,Contracts(,Grpc)}` · `Hosts/{cloud}/<M>.Host.*` · `deploy/*/<m>` · `.sln`+`.slnf` · tests | BuildingBlocks · Platform · modul lain (kecuali ref `*.Contracts`/`*.Grpc`) |
| **Cloud** | **SATU** `Platform/<App>.Platform.<Cloud>` (+ `Add<Cloud>Platform()`) · `Hosts/<Cloud>/*` · `deploy/<cloud>` · `.slnf` | **SEMUA Domain/App/Infra/Api/Contracts · BuildingBlocks · Platform cloud lain** |
| **Concern / port** | port → `BuildingBlocks.Application` · mechanism → `BuildingBlocks.Infrastructure` · impl → tiap `Platform.<Cloud>` | Domain modul · adapter tak relevan |
| **Adapter baru (concern lama)** | cuma di `Platform.<Cloud>` + config | sisanya |
| **Evolusi contract** | tambah versi di `<Producer>.Contracts` (versi lama tetap) | konsumen (opt-in saat siap) |

Pola: **modul = vertikal (ke bawah) · cloud = kolom adapter+host+IaC (ke samping) · concern = horizontal (port→agnostic→adapter).** Litmus test seam: nambah cloud ke-N menyentuh **hanya** adapter+host+IaC — nol perubahan Domain/Application.

---

## 6. Instansiasi app baru

### 6.1 Resep (dari nol ke skeleton)

1. **Scaffold** → rename `<App>`. Salin `BuildingBlocks/`, `Platform/`, `Gateway/`, `AppHost/`, `tests/Architecture`, `Directory.*.props`.
2. **Strategic DDD:** petakan domain → daftar bounded context = daftar modul. Klasifikasi **core** (full-5) · **supporting** (collapse) · **generic** (adopt off-the-shelf, jangan bikin modul penuh).
3. **Contracts:** definisikan integration event (POCO, ber-versi) di `<M>.Contracts`; `.proto` sync (bila ada) di `<M>.Grpc` terpisah.
4. **Generate hosts:** per modul × per cloud (tipis, dari template).
5. **IaC:** satu unit per service per cloud di `deploy/` — *di sinilah layanan cloud dipilih.*
6. **Kunci:** jalankan `Architecture.Tests` (6 fitness function) → hijau sebelum lanjut.

### 6.2 Yang selalu sama (jangan didesain ulang)

`BuildingBlocks.*`, `Platform.*` (semua adapter), `Platform.Hosting`, dependency rule, 6 fitness function, pola Outbox/Inbox, struktur `deploy/`. **Ini aset reusable.**

### 6.3 Yang berubah per-app

Daftar modul (bounded context), isi Domain/Application tiap modul, isi `*.Contracts`, peta produsen→konsumen.

### 6.4 Scaling ke banyak app (mis. WMS + CRP + HRMS)

> Saat app **kedua** lahir, *extract* `BuildingBlocks` + `Platform` jadi **shared package** org-level (feed internal); tiap app consume sebagai versi. Hindari copy-paste shared layer; tiap app upgrade di jadwalnya sendiri. Package & blueprint ikut **SemVer** + CHANGELOG di repo root (breaking = major bump, tiap app opt-in di jadwalnya). **Kapan TIDAK:** app pertama — biarkan in-repo sampai pola stabil (hindari abstraksi prematur).

### 6.5 Monorepo → polyrepo (saat satu app perlu pisah repo per-service)

> Unit pisah = folder `Modules/<M>` + Hosts-nya + slice `deploy/`. Saat dipisah: konsumen berhenti me-reference `<Producer>.Contracts`/`.Grpc` sebagai *project reference* dan mulai consume sebagai **versioned package** (mekanisme sama §6.4). Fitness function #3 bikin split ini murah — tak ada modul yang me-reference Domain/Application/Infrastructure modul lain, jadi cuma kontrak yang perlu jadi package. **Kapan:** beneran ada tim/cadence yang saling blok, atau jadi learning objective tersendiri.

### 6.6 Artefak akhir (opsional)

Jadikan skeleton ini **`dotnet new` template pack** → app baru jadi satu perintah CLI.

---

## 7. Referensi

**Reference app:** `dotnet/eShop` (plumbing Aspire-era: service defaults, AppHost, event bus, distributed-local) · `eShopOnContainers` (shape eksplisit: BuildingBlocks, event-bus multi-adapter, Outbox, gateway, DDD-per-service). Keduanya Azure-only — dimensi tri-cloud adalah ekstensi blueprint ini.

**Canon:** Cockburn (Hexagonal) · Martin (Clean Architecture) · Evans (DDD) · Newman (Building Microservices) · Ford et al. (Evolutionary Architectures) · Hohpe & Woolf (EIP).
