# Phase 07d — Security Hardening: Managed Identity / Workload Identity + Secret Rotation

**Status:** planned

**Pre-conditions:**
- **07a done:** authZ aktif lintas sistem (dua cloud). `IServiceTokenProvider` adapter Managed Identity (Azure, 05a) & SA OIDC (GCP, 06a) ada; `ISecretProvider` → Key Vault / Secret Manager ada; refresh-token rotation (04b) jalan; FF #9 (no `Local*` adapter di cloud host) ada.
- Penutup **Phase 07 Cross-Cutting Wide (FINAL)** — DEEP pass security; **roadmap selesai** setelah ini.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0021-service-to-service-auth.md` (s2s bearer audience-scoped; MI / SA OIDC; offline-validate; dua bidang token: s2s platform-signed vs user JWT auth-svc-signed)
- `docs/adr/0016-refresh-token-rotation.md` (rotation tervalidasi; RS256 signing key di balik `ISecretProvider`; JWKS/`kid` deferred; negative-security test behavioral)
- `docs/adr/0002-tri-cloud-hexagonal.md` (`ISecretProvider` port; SDK cloud HANYA di `Platform.<Cloud>`)

**Tujuan:** Capai posture security production-ready end-to-end di dua cloud: Managed Identity (Azure) + Workload Identity (GCP) tanpa secret di config/env; secret rotation untuk signing key + connection string; negative-security tests hijau di cloud; least-privilege RBAC/IAM.

**Deliverable:**
- Managed Identity (Azure) + Workload Identity (GCP) end-to-end — **NOL secret di config/env**: hanya `IServiceTokenProvider` real adapter (MI / WIF) yang dipakai cloud host; **FF #9 ditegakkan** (tak ada `Local*` adapter / static credential di cloud host).
- Secret rotation: Key Vault (versioning + rotation) / Secret Manager (versioning + rotation) untuk **RS256 signing key** + connection string — overlap window: key lama + baru sama-sama valid saat rotasi.
- Refresh-token rotation (ADR-0016) **tervalidasi di cloud** (bukan cuma lokal).
- Negative-security tests (`alg:none`, wrong-aud, unsigned) **hijau di cloud** — behavioral test (registry ADR-0003).
- Least-privilege RBAC (Azure) / IAM (GCP) pada resource cloud (broker, secret store, DB, registry).
- Dokumentasi item DEFERRED: JWKS endpoint + `kid` rotation, external IdP.

**Tasks:**
1. Sapu config/env: hapus semua plaintext secret; pastikan koneksi broker/DB/cache lewat MI/WIF + `ISecretProvider` (config scan).
2. Verifikasi s2s gRPC pakai MI (Azure) / SA OIDC (GCP) saja — offline-validate, audience-scoped (ADR-0021); konfirmasi FF #9 menegakkan no `Local*`/static cred di cloud host.
3. Konfigurasi rotation signing key RS256 di Key Vault / Secret Manager (versioning); host validasi key lama+baru selama overlap window.
4. Rotation connection string (Key Vault / Secret Manager versioning + reference).
5. Validasi refresh-token rotation chain + replay-defense (ADR-0016) di environment cloud.
6. Jalankan negative-security behavioral tests di cloud: token `alg:none` / wrong-aud / unsigned → ditolak.
7. Terapkan least-privilege RBAC/IAM role pada tiap resource (broker, secret store, DB, registry) — drop permission berlebih.
8. Dokumentasikan deferred: JWKS endpoint + `kid` rotation, external IdP, NoSQL projection spike, saga engine.

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF #9** hijau (no `Local*`/static credential di cloud host) + **negative-security behavioral tests** hijau (jalan terhadap cloud).
- Config scan: **nol plaintext secret**; s2s via MI/WIF saja.
- Secret rotation ter-exercise: rotate signing key → token lama+baru **sama-sama valid selama overlap window** (smoke cloud).
- Least-privilege role applied (review IAM/RBAC tiap resource).

**Learning objective:** Managed Identity / Workload Identity end-to-end; secret rotation (Key Vault / Secret Manager versioning); defense-in-depth; least-privilege; fail-secure.

**Handoff notes:** **P1 production-ready posture** lintas Local/Azure/GCP — no plaintext secret, s2s via MI/WIF, secret rotation tervalidasi, negative-security hijau di cloud, least-privilege applied. Item **DEFERRED** terdokumentasi: JWKS endpoint + `kid` rotation, external IdP, NoSQL projection spike (Cosmos/Firestore), saga engine. **Roadmap selesai.**

**Touchpoint cert:** AZ-204 — Managed Identity, Key Vault (rotation, references), secure configuration → X. PCD — Workload Identity, Secret Manager (rotation), least-privilege IAM → X.
