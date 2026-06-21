# Phase 07a — Authorization Wire-Up (deferred authZ activation)

**Status:** planned

**Pre-conditions:**
- **05d done & 06d done:** sistem penuh hidup di **kedua cloud** (Azure ACA/App Service/Functions + GCP Cloud Run/Functions). Auth (`User/Role/Permission/RefreshToken`, login JWT RS256) dari 04b ada; semua marker `// TODO-AUTH: <Module.Action>` tertanam sejak 01c→05d. `AuthorizationBehavior` masih placeholder (02a).
- Pembuka **Phase 07 Cross-Cutting Wide (FINAL)** (prinsip 5: DEEP pass setelah semua service + dua cloud jalan). 07b/07c/07d depend pada 07a.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0012-deferred-authorization-enforcement.md` (milestone Wire-Up; `IsActive` filter SEMUA mint path; warehouse-scope = operational filter BUKAN security boundary)
- `docs/adr/0016-refresh-token-rotation.md` (`IsActive` menggigit jalur credential + refresh; RS256 self-contained JWT)

**Tujuan:** Aktifkan authZ yang sengaja deferred (ADR-0012): grep semua `TODO-AUTH` → pasang `[Authorize(Permission=...)]` dengan code dari planning catalog; jadikan `AuthorizationBehavior` real (complete mediation); tutup invariant `IsActive` di SEMUA jalur mint token/claim.

**Deliverable:**
- Grep seluruh `// TODO-AUTH: <Module.Action>` → ganti dengan `[Authorize(Permission="<code>")]` (REST endpoint `*.Api`) + gerbang permission di `AuthorizationBehavior` (command/handler sensitif), code persis dari tabel `Permission` planning catalog (mis. `Inbound.PostGR`, `Outbound.DispatchWave`).
- `AuthorizationBehavior` (MediatR, `BuildingBlocks.Application`) **real** — resolve permission dari `ICurrentUser` claim, deny → `Result.Failure(Error.Unauthorized)` → 403 REST / `PermissionDenied` gRPC.
- Verifikasi `IsActive` filter di Login + Refresh + sumber claim gRPC: role/permission `IsActive=false` **tak menetes** ke JWT self-contained.
- **Warehouse-scope classification table** (per-aggregate `scoped|global`, di luar domain model) — operational filter, BUKAN security boundary; restriksi per-warehouse masa depan = permission code RBAC baru, bukan `HasQueryFilter`.
- Authz test suite di `tests/Wms.Architecture.Tests` (behavioral) + behavioral test `IsActive` non-leak.

**Tasks:**
1. Grep `// TODO-AUTH:` lintas repo → bangun checklist Module.Action → permission code (cross-check tabel `Permission`).
2. Implement `AuthorizationBehavior` real: baca required permission dari command metadata, cek vs `ICurrentUser` permissions, deny → `Error.Unauthorized`.
3. Pasang `[Authorize(Permission="...")]` di tiap REST endpoint sensitif `*.Api`; hapus marker yang sudah dipenuhi.
4. Audit jalur mint claim (Login, Refresh per ADR-0016, sumber claim gRPC) → terapkan filter `IsActive=true` pada role+permission sebelum masuk JWT.
5. Tulis warehouse-scope classification table (`scoped|global` per aggregate) sebagai artefak (di luar domain), dokumentasikan rule "per-warehouse = permission code baru".
6. Authz test suite: per Module.Action — granted → allow, denied → 403 REST / `PermissionDenied` gRPC.
7. Behavioral test: mint JWT untuk user dengan role/permission `IsActive=false` → assert permission TIDAK ada di token claim.
8. Re-run E2E existing dengan user admin → tetap hijau (regresi enforcement).

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau: **authz test suite** (permission enforced; denied → 403 REST / `PermissionDenied` gRPC) + **behavioral test** `IsActive`-false role/permission **tidak bocor** ke minted JWT.
- E2E existing (Inbound→Inventory→Outbound) dengan user admin **tetap hijau**.
- Zero `// TODO-AUTH:` tersisa untuk action yang punya permission code (sisanya tercatat sebagai gap eksplisit).

**Learning objective:** Deferred-authZ activation milestone (ADR-0012); RBAC enforcement via `[Authorize(Permission)]`; complete mediation (Saltzer & Schroeder); warehouse-scoping sebagai operational filter vs security boundary; `IsActive` sebagai invariant yang menggigit jalur credential.

**Handoff notes:** AuthZ aktif di seluruh sistem (dua cloud); `AuthorizationBehavior` real; `IsActive` non-leak terkunci; warehouse-scope classification ada sebagai operational artefak. **07b/07c/07d** (observability / resilience / security-hardening) berjalan di atas authZ yang sudah enforced. Deferred: JWKS endpoint + `kid` rotation (07d), external IdP.

**Touchpoint cert:** AZ-204 — app RBAC (`[Authorize]` policies), Microsoft identity → X. PCD — securing services, IAM → X.
