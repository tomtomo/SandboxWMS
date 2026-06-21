# ADR-0016: Refresh-token rotation (hash-only, rotation chain, replay defense)

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Auth — aggregate `RefreshToken` (tabel di Auth DB)

## Context

Access JWT berumur pendek butuh mekanisme re-issue tanpa login ulang. Refresh token yang **statis & long-lived** adalah target berisiko: bila bocor (DB compromise atau pencurian token), penyerang bisa pakai berulang tanpa terdeteksi. Butuh cara membatasi blast radius dan mendeteksi penyalahgunaan.

## Decision

- **Pilihan:** **Refresh-token rotation**. Siklus issue → rotate → revoke. **Hanya hash token** (SHA-256 dari 32-byte random) yang dipersist — token mentah tak pernah disimpan. Setiap refresh menerbitkan token baru & menandai yang lama `replacedByTokenId` (rotation chain). **Bila token yang sudah tercabut disajikan ulang → seluruh rantai dicabut** (deteksi replay). Status dihitung: `IsActive(now) = revokedAt is null && now < expiresAt`. `RefreshToken` = aggregate root tersendiri (di-query by hash tiap refresh).
- **Kenapa:** Rotation + reuse-detection adalah praktik OWASP untuk membatasi dampak token bocor; hash-only storage membatasi dampak DB compromise. `→ Canon: OWASP, Refresh Token Rotation & Session Management Cheat Sheet; Newman (Building Microservices), security & secret handling`.
- **Trade-off:** Logika refresh lebih kompleks (chain tracking, deteksi reuse, revocation cascade) dibanding token statis; perlu penyimpanan token yang di-query by hash.
- **Kapan ditinjau ulang:** Bila pindah ke identity provider eksternal (mis. Entra ID / Identity Server) yang menangani rotation → aggregate ini bisa disederhanakan/ditiadakan.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Rotation + hash-only + reuse-detection chain** *(dipilih)* | Blast radius kecil; replay terdeteksi; DB-leak terbatas | Logika refresh kompleks | OWASP (Refresh Token Rotation) |
| B. Refresh token statis long-lived (stored) | Sederhana | Token bocor = akses tak terbatas & tak terdeteksi | OWASP (Session Mgmt) |
| C. Tanpa refresh token (re-login tiap expiry) | Paling sederhana & aman | UX buruk; tak melatih pola token | — |

## Consequences

**Positif**
- DB compromise tak membocorkan token usable (hanya hash); token bocor punya window sempit + terdeteksi saat reuse.
- Memberi latihan konkret pola identity production-grade (selaras tujuan AZ-204 security topics).

**Trade-off / lebih sulit**
- Perlu uji menyeluruh untuk edge case rotation (refresh konkuren, clock skew, revoke cascade).

**Yang harus dijaga**
- Token mentah **tak pernah** dipersist; perbandingan selalu via hash. Revocation cascade harus atomic terhadap chain.

## Out of scope / deferred

- Sliding expiration, device/session binding, dan multi-device token management belum di-scope.
- Integrasi external IdP (Entra ID/OAuth provider) di-defer; saat ini Auth = authority lokal ([ADR-0011](0011-master-data-read-api-cache-aside.md)).

## Amendment — 2026-06-20

> Rotation refresh-token di atas tetap. Blok ini menutup dua item yang sebelumnya dibiarkan terbuka: **password hashing** & **algoritma signing JWT**.

- **Password hashing di `IPasswordHasher`** (port [ADR-0002](0002-tri-cloud-hexagonal.md)): KDF lambat & salted di balik **format opaque self-describing** `{algo}.{iter}.{salt}.{hash}` (kolom `PasswordHash` opaque ke domain) → migrasi algoritma tanpa schema change; constant-time compare; rehash-on-upgrade; **timing-safe login** (dummy verify vs sentinel hash saat user tak dikenal — anti user-enumeration). Algoritma = **Argon2id** (sebagai parameter, bukan di-hard-mandate).
- **JWT signing = RS256 (asymmetric)**: auth-svc menandatangani dgn private key (di balik `ISecretProvider`); host lain hanya pegang public key & **verify offline**. **Alg-pinning** (`ValidAlgorithms=[RS256]`, tolak `HS256`/`none` — anti alg-confusion); validasi iss/aud/exp/nbf; fail-secure startup (key kosong → fail fast); satu helper validasi bersama dipakai semua host. Dipakai juga s2s ([ADR-0021](0021-service-to-service-auth.md)). **Negative-security test** (token unsigned / wrong-aud / `alg:none` ditolak) = **test behavioral di test suite**, terdaftar di registry [ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md) (bukan NetArchTest static). `→ Canon: OWASP ASVS, JWT/JWS validation`.
- JWKS endpoint + `kid` rotation tetap **deferred** (konsumen eksternal / rotasi dinamis) — dicatat sebagai revisit seam. **RS256 local-signing = otoritas aktif sekarang**; delegasi ke external IdP tetap *deferred* (selaras 'Kapan ditinjau ulang' & Out-of-scope di atas).
