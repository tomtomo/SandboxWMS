# ADR-0022: Operational audit log (action-level, outcome-aware, survive-rollback)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Port `IAuditLogStore`, `AuditLogBehavior` (MediatR pipeline), schema `infrastructure` per service

## Context

`IAuditable` (`createdBy`/`modifiedBy`, [overview](../tomsandboxwms-overview.md)) menjawab "siapa terakhir mengubah X" — **bukan** timeline per-aktor atau forensik atas **percobaan yang ditolak**. Tindakan sensitif (post GR, dispatch wave) butuh jejak aksi yang append-only, termasuk **kegagalan**. Tegangan kunci: jika audit ditulis dalam transaksi bisnis dan command `Result.Failure` → rollback, baris audit ikut hilang — padahal audit-of-attempt-yang-gagal justru inti forensiknya.

## Decision

- **Pilihan:** **Append-only `AuditLogEntry`** via satu **`AuditLogBehavior`** (MediatR), hanya untuk `*Command` yang mutasi.
  - **Outcome-aware**: rekam `IsSuccess` + `ErrorCode` (dari `Result`, [ADR-0019](0019-error-handling-result-transport-mapping.md)).
  - **Write out-of-band**: audit ditulis lewat `IAuditLogStore` di **koneksi/transaksi sendiri**, sehingga **survive rollback** transaksi bisnis (best-effort durability + retry). **Bukan via Outbox** — Outbox commit satu-tx dengan state, jadi akan ikut ter-rollback dan menggagalkan tujuan forensik.
  - **SYSTEM actor** ([ADR-0027](0027-system-actor-convention.md)) untuk origin non-HTTP; satu predikat PII-redaction bersama; `IAuditableCommand` eksplisit untuk `AggregateType`/`Id` (bukan reflection).
  - Read-side handler ada tapi **dorman** (tak ada host mount, tak ada service khusus).
- **Kenapa:** Forensik membutuhkan jejak yang independen dari hasil transaksi bisnis; out-of-band adalah satu-satunya cara merekam attempt yang gagal. `→ Canon: Fowler (Analysis Patterns), Audit Log; Hohpe & Woolf (EIP), Message Store; Newman (Building Microservices), auditability`.
- **Trade-off (sadar):** Audit-write tak atomic dengan state bisnis (ada window kecil: state commit tapi audit-write gagal). Untuk audit-log ini diterima (mitigasi: retry/durability pada store); jalur upgrade hybrid (success → in-band atomic; failure → out-of-band) dicatat sebagai opsi.
- **Kapan ditinjau ulang:** Bila regulasi menuntut audit **atomic & non-loss** untuk operasi sukses → pindah ke pola hybrid.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Out-of-band, outcome-aware, survive-rollback** *(dipilih)* | Merekam attempt gagal (forensik utuh); sederhana | Tak atomic dgn state (best-effort) | Fowler (Analysis Patterns), Audit Log |
| B. In-band (audit di transaksi bisnis) | Atomic saat sukses | **Hilang saat rollback** — gagal merekam attempt yang ditolak | Fowler (Analysis Patterns) |
| C. Via Outbox | Reuse rails EDA | Ikut rollback dgn state → sama buruknya dgn B untuk failure | Hohpe & Woolf (EIP) |
| D. Hybrid (sukses in-band, gagal out-of-band) | Paling benar | Paling kompleks; dua jalur write | Fowler (Analysis Patterns) |

## Consequences

**Positif**
- Timeline per-aktor + forensik atas percobaan yang ditolak; fit idiomatik MediatR/CQRS ([ADR-0004](0004-cqrs-vertical-slice.md)).
- Memakai identity dari authN ([ADR-0012](0012-deferred-authorization-enforcement.md)) & SYSTEM actor ([ADR-0027](0027-system-actor-convention.md)).

**Trade-off / lebih sulit**
- Store audit perlu kebijakan resilience/retry sendiri agar window non-atomic tak menelan event.
- Slot authz di behavior = placeholder `TODO-AUTH` (authZ deferred, [ADR-0012](0012-deferred-authorization-enforcement.md)).

**Yang harus dijaga**
- Audit append-only & immutable; `AggregateType`/`Id` via `IAuditableCommand` eksplisit (test perilaku menegakkan behavior teraudit).

## Out of scope / deferred

- Read/query UI audit, retensi & arsip jangka panjang → deferred (handler dorman dulu).
- Pola hybrid (atomic-on-success) → upgrade path.
