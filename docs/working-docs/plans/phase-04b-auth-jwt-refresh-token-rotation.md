# Phase 04b — Auth: JWT RS256 + Refresh-Token Rotation + Argon2id

**Status:** done (2026-06-22)

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

**Handoff notes (done 2026-06-22):**
authN NYALA — identitas nyata mengalir ke `ICurrentUser`/audit; Auth read-API (User/Role/Permission) + offline JWT helper siap. **244 test hijau** (sebelumnya 194 → +50: Auth.Domain 38, Auth.Integration 7, negative-security 5). `dotnet build Wms.sln` 0/0. Migration `InitialAuth` apply bersih di Postgres riil (Testcontainers).

**Dibangun:**
- **Modul `Wms.Auth` FULL-6** (Domain/Application/Infrastructure/Api/Contracts/Grpc) + `Host.Local`. Aggregate `User`(Active/Locked/Disabled, lockout, rehash) / `Role`(RBAC + permission codes) / `RefreshToken`(rotation chain, hash-only, IsActive terhitung) + `Permission` (reference entity, code natural key). **TDD domain RED→GREEN** (38 test). Slice **Login** (timing-safe dummy-verify, rehash-on-upgrade, lockout) / **Refresh** (rotate `replacedByTokenId` + reuse-detection cascade) / **Logout** (revoke idempotent). gRPC read-API `GetUser/GetRole/GetPermission` (reader-delegation + cache-aside `CachedAuthReader`, FF#8) — GetUser claim-source filter role aktif (ADR-0012). REST `/auth/login|refresh|logout` + diagnostic `/auth/me`.
- **Port BARU:** `IPasswordHasher` (+`PasswordVerificationResult`, `Sentinel`) & `ISecretProvider` di BuildingBlocks.Application (sebelumnya hanya di naming-registry, BELUM ada file). `WmsClaims`/`WmsJwtDefaults`/`AuthSecretNames` (shared constants). **`WmsJwtBearer`** (BuildingBlocks.Web) = offline validation helper alg-pin RS256 (`AddWmsJwtBearer` + `BuildValidationParameters` murni). Adapter Platform.Local: `Argon2idPasswordHasher` (Konscious, OWASP param, opaque format, constant-time, rehash, sentinel) + `LocalSecretProvider` (env `Secrets__{name}` + ephemeral RSA fallback) + `AddLocalPasswordHasher`/`AddLocalSecretProvider`.
- **Wire JWT bearer ke SEMUA host** (Inbound/Inventory/Outbound/MasterData/Auth): `AddLocalSecretProvider`+`AddWmsJwtBearer`+`UseAuthentication` → token valid mengisi `HttpContext.User` → `ICurrentUser` identitas nyata (ganti anonymous/SYSTEM-actor). **AppHost** generate dev RSA keypair ephemeral per-run, distribusi via env (private→auth, public→semua). `authdb` + host `auth`. **MigrationRunner** +`AuthDbContext` apply + seed (permission catalog + Admin role + 1 admin user, idempotent; ⚠ password dev-default `ChangeMe123!` overridable config).
- `Permission` planning catalog di-seed (16 code overview §E, **NOT enforced** — authZ deferred ADR-0012). Negative-security behavioral test (alg:none/HS256/wrong-aud ditolak, RS256 diterima, key-kosong fail-secure) di `Architecture.Tests`. Auth = full-6 di `ModuleLayers` FF (FF#1–8/#11 hijau).

**Keputusan load-bearing / utang sadar (flag):**
1. **Security side-effect di failure-path = OUT-OF-BAND** (PENTING, bug ketemu+fix): `TransactionBehavior` rollback saat `Result.Failure` → cascade-revoke (reuse) & failed-login-increment (lockout) AWALNYA ter-rollback (replay-defense bocor, lockout tak pernah aktif). Fix: tulis di **scope/DbContext SEGAR** (`IServiceScopeFactory`, pola `AuditLogBehavior` ADR-0022) → commit lepas dari rollback command. Regression guard: test `Repeated_failed_logins_lock_the_account` + cascade step-4.
2. **Audit createdBy = identitas nyata** dibuktikan via **identity-flow** (`/auth/me` ber-bearer → `ICurrentUser`=adminId, bukan anonymous) + interceptor IAuditable pre-tested (Phase 02c) — komposisi, BUKAN satu test cross-host literal (auth-host tak punya auditable-write di-belakang-auth; hindari kompleksitas/flaky env-keypair cross-host).
3. **Koleksi strongly-typed-id**: EF mendiskualifikasi `List<RoleId>` sebagai navigation→entity. Fix: backing `_roleIds` = `List<Guid>` (project ke RoleId di accessor) + `Ignore` computed accessor + JSON-text converter (RoleIds/WarehouseIds/PermissionCodes). Tak di-query ke dalam (round-trip mint saja).
4. **Warehouse-scoping di-MODEL** (User.AssignedWarehouseIds + embed claim) tapi **enforcement DEFERRED** (ADR-0012 → 07a). **Live Aspire cross-host = MANUAL** (butuh Docker; keypair distribusi via AppHost env sudah jalan, tapi belum Claude-run); E2E otoritatif via integration 1-proses. **authZ enforcement deferred** — no `[Authorize]`; permission catalog planning-only.

**CPM +:** Konscious.Security.Cryptography.Argon2 1.3.1, Microsoft.AspNetCore.Authentication.JwtBearer 8.0.15, System.IdentityModel.Tokens.Jwt + Microsoft.IdentityModel.Tokens 7.1.2 (match closure JwtBearer).

**Next:** **04c** (`reporting-projections`, depends-on 03c ✓) atau **04d** (`notification-async-delivery`, depends-on 04b ✓ — konsumsi Auth read-API `GetUser` untuk recipient detail). `IPasswordHasher`/`ISecretProvider`/`WmsJwtBearer` siap reuse.

**Note:** ⚠ prinsip-3 exception — Auth (supporting) merealisasikan authN identity yang audit/SYSTEM-actor asumsikan sejak Phase 02; justified karena mendasari cross-service identity (ADR-0021 user-JWT plane) + integration tests berikutnya.

**Touchpoint cert:** AZ-204 — Microsoft identity platform / JWT + Key Vault (signing key via `ISecretProvider`; branded 05/07). PCD — Secret Manager + JWT/JWS validation (branded 06/07).
