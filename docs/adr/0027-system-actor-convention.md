# ADR-0027: SYSTEM actor convention untuk operasi non-HTTP

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** `ICurrentUser` (Application-layer); konsumen bus, background job, seeder, service-to-service ([ADR-0021](0021-service-to-service-auth.md)), audit-log ([ADR-0022](0022-operational-audit-log.md))

## Context

Identitas (`ICurrentUser`) mengalir dari JWT untuk request HTTP ([ADR-0012](0012-deferred-authorization-enforcement.md)), tapi banyak operasi **tak punya HttpContext**: handler integration-event, background job (quarantine-aging, [ADR-0025](0025-cross-cutting-platform-ports.md)), seeder, dan call service-to-service ([ADR-0021](0021-service-to-service-auth.md)). Tanpa konvensi, `IAuditable`/audit-log ([ADR-0022](0022-operational-audit-log.md)) tak punya principal untuk write origin-mesin — dan ada footgun keamanan bila salah memetakan "anonim" jadi "SYSTEM".

## Decision

- **Pilihan:** `ICurrentUser` me-resolve sebagai **SYSTEM** ketika **`HttpContext` null** (consumer bus / background job / seeder / s2s). Keputusan **di-key pada `HttpContext`-is-null**, **BUKAN** pada `!IsAuthenticated`. Invariant keamanan dipancang oleh unit test: **anonymous HTTP** (HttpContext ada, `IsAuthenticated=false`) **tak boleh** mengambil cabang SYSTEM.
- **Kenapa:** Memberi principal yang prinsipil untuk write origin-mesin (dipakai `IAuditable`, audit-log, s2s) tanpa membuka celah privilege. Membedakan "tak ada konteks request" (mesin) dari "request tak terotentikasi" (anonim) adalah inti yang mencegah kebocoran. `→ Canon: Newman (Building Microservices), identity/security context propagation; OWASP ASVS, access-control context`.
- **Trade-off:** Satu invariant tambahan yang harus dites; semua jalur non-HTTP harus konsisten memakai konvensi.
- **Kapan ditinjau ulang:** Bila SYSTEM perlu di-granularisasi (mis. principal berbeda per job/consumer untuk audit) → perkaya jadi beberapa machine-principal.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. SYSTEM saat HttpContext null + invariant anon≠SYSTEM** *(dipilih)* | Principal jelas untuk mesin; cegah privilege-leak; reusable | Satu invariant untuk dijaga | Newman (Building Microservices); OWASP ASVS |
| B. SYSTEM saat `!IsAuthenticated` | Lebih sederhana | **Anonymous HTTP jadi SYSTEM** → kebocoran lintas-warehouse | OWASP ASVS |
| C. Tiap caller pass actor manual | Eksplisit | Mudah lupa; tak konsisten; rawan salah | Richards & Ford (Fundamentals) |

## Consequences

**Positif**
- `IAuditable` & audit-log ([ADR-0022](0022-operational-audit-log.md)) punya actor untuk write origin-mesin; dipakai ulang oleh consumer/job/s2s ([ADR-0021](0021-service-to-service-auth.md)).
- Invariant anon≠SYSTEM mencegah footgun kebocoran data lintas-warehouse.

**Trade-off / lebih sulit**
- Semua jalur non-HTTP harus melewati `ICurrentUser` yang konsisten (bukan principal ad-hoc).

**Yang harus dijaga**
- Pure Application-layer (nol transport/SDK); invariant anon≠SYSTEM ditegakkan unit test.

## Out of scope / deferred

- Tidak digandeng ke **warehouse-scoping** (yang tetap deferred ke Authorization Wire-Up, [ADR-0012](0012-deferred-authorization-enforcement.md)).
- Granularisasi machine-principal per job/consumer → deferred sampai dibutuhkan audit yang lebih halus.
