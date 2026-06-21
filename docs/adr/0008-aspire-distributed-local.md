# ADR-0008: Orkestrasi distributed-local via .NET Aspire (AppHost)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** `AppHost/<App>.AppHost`, `Platform.Hosting` (service defaults), environment Local

## Context

Microservices ([ADR-0001](0001-microservices-from-start.md)) berarti banyak proses + dependency (DB per service, broker, gateway, object storage) yang harus jalan bareng saat dev lokal. Menjalankannya manual (banyak terminal, wiring port/connection string by hand) rapuh dan menyita waktu. Local adalah environment **pertama** (sebelum Azure/GCP) jadi harus mulus.

## Decision

- **Pilihan:** Pakai **.NET Aspire** sebagai orkestrator **lokal** — `<App>.AppHost` mendeklarasikan semua host + dependency dan men-wire-nya; `Platform.Hosting` menyediakan **service defaults** agnostic (logging, telemetry, health) yang dipakai semua host.
- **Kenapa:** Aspire adalah model distributed-local idiomatik .NET era sekarang (dipakai reference app `dotnet/eShop`): satu F5 menyalakan seluruh topologi + dashboard observability. Service defaults memusatkan cross-cutting tanpa cloud SDK. `→ Canon: Newman (Building Microservices), developer experience & deployment; referensi: dotnet/eShop (host model)`.
- **Trade-off:** Aspire = teknologi .NET-spesifik & relatif baru; ini wiring **lokal** saja — produksi tetap IaC per-cloud (`deploy/`), bukan Aspire.
- **Kapan ditinjau ulang:** Bila perlu paritas lebih tinggi dengan runtime produksi lokal (mis. emulasi penuh layanan cloud) → tambahkan emulator/containers spesifik.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. .NET Aspire AppHost** *(dipilih)* | Satu-perintah jalan; dashboard + service discovery; idiomatik .NET (eShop) | .NET-spesifik & baru; lokal-only | Newman (Building Microservices); eShop |
| B. docker-compose manual | Netral bahasa; eksplisit | Wiring & service discovery manual; tak ada dashboard terpadu | — |
| C. Jalankan tiap host manual (banyak terminal) | Nol tooling | Rapuh, lambat, tak skala ke 7 service | — |

## Consequences

**Positif**
- Onboarding lokal = satu perintah; dependency (DB, broker, storage) ikut ter-provision untuk dev.
- Service defaults (`Platform.Hosting`) konsisten lintas host & **nol cloud SDK** — selaras fitness function #1 ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)).

**Trade-off / lebih sulit**
- Pengetahuan Aspire jadi prasyarat dev lokal; tipe host produksi tetap heterogen ([ADR-0018](0018-compute-hosting-mixed-paas.md)) — Aspire tak menyetir produksi.

**Yang harus dijaga**
- AppHost murni **orkestrasi lokal**; pemilihan layanan cloud tetap hidup di `deploy/` ([ADR-0002](0002-tri-cloud-hexagonal.md)), bukan di AppHost.

## Out of scope / deferred

- Deployment Aspire ke Azure Container Apps (Aspire manifest → ACA) bisa dieksplor nanti; saat ini produksi via Bicep/Terraform.
