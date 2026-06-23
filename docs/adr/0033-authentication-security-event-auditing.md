# ADR-0033: Auditing event autentikasi & keamanan (Login / Refresh / Logout + reuse-detection)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-24
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Auth — command Login/Refresh/Logout ([ADR-0016](0016-refresh-token-rotation.md)); operational audit log ([ADR-0022](0022-operational-audit-log.md)); pipeline behavior ([ADR-0004](0004-cqrs-vertical-slice.md))

## Context

Audit log operasional ([ADR-0022](0022-operational-audit-log.md)) bersifat **append-only, outcome-aware, out-of-band** (survive rollback), **opt-in** via `IAuditableCommand`. Tapi justru tiga aksi **paling relevan-keamanan** — `Login`, `Refresh`, `Logout` — **tidak** ditandai `IAuditableCommand` → nol jejak di audit log. `LoginCommand` bahkan secara sadar dikomentari "tak auditable (pre-auth → anonymous)". Konsekuensinya:

- **Login attempt** (sukses, gagal-kredensial, lockout) tak terekam → tak ada timeline forensik "siapa mencoba masuk kapan".
- **Refresh reuse-detection cascade** (`RefreshHandler.CascadeRevokeOutOfBandAsync`) — sinyal **kompromi token** yang justru audit log ada untuk menangkapnya — **invisible**: command mengembalikan `Failure(refresh_token.not_active)` tanpa jejak audit.
- **Logout** (pencabutan sesi) tak terekam.

Ini **melemahkan tujuan forensik** ADR-0022 di area yang paling membutuhkannya (OWASP ASVS V7: authentication & session events WAJIB di-log). `AuditLogBehavior` sudah menulis **even on Failure** out-of-band; `AuditRedaction` sudah meredaksi field sensitif (`password`/`token`/`secret`/…) by property-name substring — jadi infrastruktur untuk mengaudit ini **sudah ada**, hanya belum di-*opt-in*.

## Decision

- **Pilihan:** Tandai `LoginCommand`, `RefreshCommand`, `LogoutCommand` sebagai **`IAuditableCommand`**. Tak ada mekanisme baru — `AuditLogBehavior` ([ADR-0022](0022-operational-audit-log.md)) menangkap `actor` (`ICurrentUser`, anonymous saat pre-auth — itu fakta yang dicatat, bukan penghalang), `action` (nama command), `outcome` (`IsSuccess`/`ErrorCode`), `payload` ter-redaksi, `traceparent` — **pada sukses MAUPUN gagal**, out-of-band (survive rollback transaksi command). Reuse-detection ter-audit otomatis: `RefreshCommand` yang men-trigger cascade mengembalikan `Failure(refresh_token.not_active)` → satu baris audit `IsSuccess=false, ErrorCode=refresh_token.not_active`. Kredensial (`Password`, `RefreshToken`) **wajib & otomatis ter-redaksi** oleh `AuditRedaction`.
  - **`AggregateType`/`AggregateId`:** `Login` → `User`/`Username` (kunci forensik human-readable, non-rahasia). `Refresh`/`Logout` → `RefreshToken`/**string kosong** — token mentah **rahasia** (tak boleh masuk kolom `AggregateId` yang tak ter-redaksi) dan hash-nya dihitung downstream (handler, via generator — menyalinnya ke command = coupling algoritma hash); event-level audit (actor + action + outcome + traceparent + timeline `RefreshToken.RevokedAt`) sudah memenuhi intent. Korelasi per-token granular = security event lebih kaya, di-defer (Phase 07).
- **Kenapa:** Event autentikasi/keamanan adalah persis yang audit-log forensik ada untuk menangkapnya; meninggalkannya opt-out membuat ADR-0022 buta di titik tergelap. Reuse-detection cascade = sinyal kompromi yang HARUS terekam meski command Failure — pola out-of-band ADR-0022 menjaminnya tanpa kode tambahan. `→ Canon: OWASP ASVS V7 (Logging & Error Handling — log authentication decisions & session lifecycle); Fowler (Analysis Patterns), Audit Log; ADR-0022 intent.`
- **Trade-off:** Volume audit naik (trafik login, termasuk gagal/lockout berulang) — dapat di-tune retention (ADR-0022 out-of-scope). Redaksi kredensial = **wajib** (sudah ada; di-test). Granularitas cascade = command-level (satu baris per refresh-reuse, bukan per-token-dalam-rantai) — cukup untuk forensik dasar; rantai lengkap dapat ditelusuri via timeline `RevokedAt`.
- **Kapan ditinjau ulang:** Bila butuh security event terstruktur (geo/IP, panjang rantai cascade, real-time alerting/SIEM) → emit **dedicated security-audit event/stream** terpisah dari `audit_log` operasional (Phase 07 observability). Bila pindah ke external IdP ([ADR-0016](0016-refresh-token-rotation.md) "kapan ditinjau") → audit auth di-delegasi ke IdP.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Tandai 3 command `IAuditableCommand`, reuse `AuditLogBehavior`** *(dipilih)* | Nol mekanisme baru; out-of-band + redaction + outcome-aware sudah ada; reuse-detection ter-audit otomatis | Granularitas command-level; volume login naik | OWASP ASVS V7; ADR-0022 |
| B. Dedicated security-audit event/stream (tipe + skema sendiri) | Forensik kaya (chain extent, IP, alerting) | Skema/infra baru; lebih dari yang dibutuhkan sekarang | OWASP; Bellemare (event) |
| C. Biarkan (status quo) | Nol kerja | Buta forensik di titik paling security-relevant; langgar ASVS V7 | — |

## Consequences

**Positif**
- Timeline forensik lengkap: login attempt (sukses/gagal/lockout), refresh (termasuk **reuse-detection cascade**), logout — semua di append-only `audit_log`.
- Memperkuat tujuan ADR-0022 di area tergelap tanpa kode mekanisme baru.

**Trade-off / lebih sulit**
- Volume audit naik (login traffic) — retention/rotation jadi concern operasional (ADR-0022 deferred).

**Yang harus dijaga**
- **Redaksi kredensial wajib**: `Password` (Login) & `RefreshToken` (Refresh/Logout) **tak pernah** plaintext di `audit_log` — dijaga `AuditRedaction` + test eksplisit.
- Reuse-detection tetap ter-audit **meski command Failure** (pola out-of-band ADR-0022; jangan pindahkan audit ke dalam transaksi command).
- `AggregateId` Refresh/Logout = string kosong (bukan token/prefix) — jangan bocorkan material token ke kolom tak-ter-redaksi.

## Out of scope / deferred

- Security event terstruktur (IP/geo, panjang rantai cascade, severity), real-time alerting/SIEM → Phase 07.
- Audit volume retention/rotation policy → operasional (ADR-0022 sudah catat deferred).
- Atribusi aktor non-anonymous saat login (pre-auth selalu anonymous by design, [ADR-0027](0027-system-actor-convention.md)).
