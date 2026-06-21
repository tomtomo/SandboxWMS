# Phase 04b — Auth: JWT RS256 + Refresh-Token Rotation + Argon2id

**Status:** planned

**Pre-conditions:**
- **04a done:** gRPC read-API + cache-aside + `ResiliencePipelineDefaults` + `IServiceTokenProvider` pattern terpasang; MasterData hidup di Aspire.
- `IPasswordHasher` & `ISecretProvider` ports sudah ada di registry (ADR-0002/0016); host masih pakai SYSTEM-actor untuk `ICurrentUser` (audit createdBy = SYSTEM).

**Context refs (WAJIB):**
- `docs/adr/0016-refresh-token-rotation.md` (rotation chain + hash-only + Argon2id + RS256 alg-pinning) · `docs/adr/0012-deferred-authorization-enforcement.md` (authN aktif, authZ deferred, `IsActive` filter di jalur mint)
- `docs/adr/0011-master-data-read-api-cache-aside.md` (read-API + cache-aside pattern, reused) · `docs/tomsandboxwms-overview.md` §E

**Tujuan:** Realisasikan authentication: login (JWT RS256 + refresh), rotation chain dengan reuse-detection, Argon2id hashing, offline JWT validation alg-pinned di semua host — sehingga `ICurrentUser` dapat identitas nyata (ganti SYSTEM-for-everything).

**Deliverable:**
- `Wms.Auth.{Domain,Application,Infrastructure,Api,Contracts,Grpc}` (collapsed). Aggregate `User`(Active/Locked/Disabled) / `Role` / `RefreshToken` + `Permission` (reference entity).
- Slices **Login** (verify via `IPasswordHasher` Argon2id → issue access JWT RS256 + refresh token) / **Refresh** (rotation chain via `replacedByTokenId`; reuse-detection → cascade revoke seluruh rantai, atomic) / **Logout** (revoke).
- `IPasswordHasher` port + adapter Argon2id: format opaque `{algo}.{iter}.{salt}.{hash}`, constant-time compare, rehash-on-upgrade, **timing-safe login** (dummy verify vs sentinel saat user tak dikenal).
- RS256 signing (private key via `ISecretProvider` Local) + **satu shared offline JWT validation helper** (alg-pinning `ValidAlgorithms=[RS256]`, tolak `alg:none`/HS256, validasi iss/aud/exp/nbf, fail-fast key kosong) dipakai semua host.
- `Permission` planning catalog di-seed (`Inbound.PostGR` dst — NOT enforced; authZ deferred).
- `Wms.Auth.Grpc` read-API (User/Role/Permission) + **`IsActive` filter di SEMUA jalur mint token/claim**.
- Wire JWT bearer ke hosts → `ICurrentUser` dapat identitas nyata (ganti SYSTEM); audit `createdBy` nyata. Seed 1 user admin.

**Tasks:**
1. Aggregate `User`/`Role`/`RefreshToken` + `Permission` reference entity; DbContext schema `auth`; soft-delete `isActive`.
2. `IPasswordHasher` Argon2id adapter (opaque format, constant-time, rehash-on-upgrade, sentinel dummy-verify).
3. RS256 sign via `ISecretProvider`; shared offline validation helper (alg-pin, iss/aud/exp/nbf, fail-fast).
4. Slice Login → access JWT + refresh; Refresh → rotate (`replacedByTokenId`) + reuse-detection cascade revoke (atomic); Logout → revoke.
5. Seed `Permission` planning catalog (NOT wired) + 1 admin user.
6. `Wms.Auth.Grpc` read-API (User/Role/Permission) + `IsActive` filter di semua mint path (Login/Refresh/claim-source gRPC).
7. Wire JWT bearer (shared helper) ke semua host; `ICurrentUser` ambil identitas nyata; `Wms.Auth.Host.Local` declare di `Wms.AppHost`.
8. Negative-security behavioral tests (`alg:none`/wrong-aud/unsigned ditolak) di `tests/Wms.Architecture.Tests`.

**Definition of Done:**
- `dotnet build Wms.sln` hijau; **semua FF + negative-security behavioral tests hijau**.
- Integration: login → access+refresh JWT terbit; refresh **rotasi** (token lama revoked); token lama yang sudah tercabut disajikan ulang → **seluruh rantai revoked**; audit record `createdBy` = identitas nyata (bukan SYSTEM).

**Out-of-scope:** `[Authorize(Permission=...)]` enforcement + warehouse-scope (deferred → Phase 07a). JWKS endpoint / `kid` rotation. External IdP / OIDC token-exchange. Key Vault/Secret Manager branded (Phase 05/06/07).

**Learning objective:** Refresh-token rotation (OWASP) + reuse-detection cascade, hash-only storage + Argon2id KDF + timing-safe login, RS256 asymmetric + offline JWT validation + alg-pinning (anti alg-confusion), deferred-authZ planning catalog, authentication-active identity flow.

**Handoff notes:** authN nyala — identitas nyata mengalir ke `ICurrentUser`/audit; Auth read-API (User/Role) + offline JWT helper siap. **04d** Notification konsumsi Auth read-API (recipient detail). authZ enforcement tetap deferred (TODO-AUTH markers menunggu Phase 07a).

**Note:** ⚠ prinsip-3 exception — Auth (supporting) merealisasikan authN identity yang audit/SYSTEM-actor asumsikan sejak Phase 02; justified karena mendasari cross-service identity (ADR-0021 user-JWT plane) + integration tests berikutnya.

**Touchpoint cert:** AZ-204 — Microsoft identity platform / JWT + Key Vault (signing key via `ISecretProvider`; branded 05/07). PCD — Secret Manager + JWT/JWS validation (branded 06/07).
