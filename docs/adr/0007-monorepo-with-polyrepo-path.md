# ADR-0007: Monorepo (Level 1) dengan jalur polyrepo yang sudah disiapkan

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Repo root, `*.Contracts`/`*.Grpc` sebagai project reference vs versioned package

## Context

Microservices ([ADR-0001](0001-microservices-from-start.md)) tidak mengharuskan satu-repo-per-service. Untuk pengembang solo, polyrepo sejak awal menambah friksi (versioning silang, koordinasi PR lintas-repo, CI berlipat) tanpa manfaat — manfaat polyrepo (otonomi cadence/tim) baru muncul saat ada tim yang saling blok.

## Decision

- **Pilihan:** Mulai sebagai **monorepo (Level 1)** — semua service, BuildingBlocks, Platform, deploy dalam satu repo; antar-modul consume `*.Contracts`/`*.Grpc` sebagai **project reference**. **Siapkan jalur polyrepo**: saat sebuah service perlu pisah, konsumen beralih dari project reference ke **versioned package** (SemVer + CHANGELOG), unit pisah = folder `Modules/<M>` + Hosts + slice `deploy/`.
- **Kenapa:** Default termurah untuk solo; refactor lintas-service atomic dalam satu commit. Fitness function #3 ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)) menjamin tak ada coupling internal antar-modul, jadi **split jadi murah** — hanya kontrak yang perlu jadi package. `→ Canon: Newman (Building Microservices), repo & deployment; Newman (Monolith to Microservices), ekstraksi bertahap`.
- **Trade-off:** Satu repo besar; build/CI perlu filter fokus (`.slnf` per-env) agar tak selalu build semua.
- **Kapan ditinjau ulang:** Saat ada tim/cadence yang **nyata saling blok**, atau polyrepo jadi learning objective tersendiri → pisahkan service tertentu.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Monorepo + jalur polyrepo siap** *(dipilih)* | Termurah untuk solo; refactor atomic; split murah berkat FF #3 | Repo besar; perlu solution filter | Newman (Building Microservices) |
| B. Polyrepo sejak awal (1 repo/service) | Otonomi cadence penuh | Versioning silang & koordinasi mahal tanpa tim; premature | Newman (Monolith to Microservices) |
| C. Monorepo tanpa boundary kontrak (shared internal) | Paling sedikit setup | Mustahil dipisah nanti; melawan ADR-0010 | Ford et al. (Hard Parts) |

## Consequences

**Positif**
- Perubahan kontrak + produsen + konsumen bisa satu PR (selama masih monorepo).
- Transisi ke polyrepo terdefinisi & murah — mekanisme package sama dengan multi-app extraction (blueprint §6.4/§6.5).

**Trade-off / lebih sulit**
- Disiplin solution filter (`<App>.Local/Azure/Gcp.slnf`) diperlukan agar fokus build per-environment.

**Yang harus dijaga**
- Antar-modul **hanya** lewat `*.Contracts`/`*.Grpc`, tak pernah project internal — agar litmus split tetap berlaku ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md) FF #3).

## Out of scope / deferred

- Ekstraksi BuildingBlocks/Platform jadi shared package org-level (blueprint §6.4) di-defer sampai app kedua lahir — hindari abstraksi prematur.
- Pemilihan registry package internal (feed) belum dirinci.

## Amendment — 2026-06-20

> Melengkapi mekanisme version-governance monorepo yang sebelumnya tak dinamai.

- **Central Package Management (CPM)**: semua versi dipin di `Directory.Packages.props` (+ transitive pinning); `Directory.Build.props` memegang baseline .NET 8 LTS (nullable + implicit usings). Satu locus upgrade untuk monorepo; jadi locus tunggal penempatan shared-dependency — mis. Polly ([ADR-0020](0020-resilience-pipeline-defaults.md)). Build/version-governance hygiene (dampak arsitektur minimal, tapi bukan nol karena menetapkan konvensi penempatan dependency bersama).
