# Session 04 — Security & Auth · Compliance & Data Governance

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **dimension-specific scrutiny** for this session.

- **Session code / partial filename:** `04-security-and-compliance`
- **Finding ID prefix:** `SEC` (e.g. `SEC-001`)
- **Checklist sections covered:** §7 Security & Auth · §17 Compliance & Data Governance · the **data-isolation surface** of §16 Multi-Tenancy (from `tech-checklist-blank.md`)
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-04-security-and-compliance.md` (WMS repo root)

This is the **trust-boundary** session: who is allowed to do what (authentication, authorization, secrets, transport), how the system resists the adverse user (OWASP Top 10, rate limiting, CORS, headers), and how it accounts for what it holds (PII handling, audit logging, retention, residency). Security and compliance are clustered because their concerns physically overlap in the code — **password hashing, PII handling, and audit logging appear in both §7 and §17** — so assess each shared concern *once* and cross-reference rather than double-counting. Multi-Tenancy (§16) is marked **"Belum direncanakan"** (not planned), but **data isolation is a security property, not a feature**: warehouse-scoping and row-level security decide whether one tenant can read another's stock, so this session checks whether isolation *exists at the query level*. Any nascent tenancy/isolation code present despite the "not planned" status is itself a **silent-decision** finding (Method 1) — surface it and record it for session 99.

---

## Scope — checklist items to verdict

### §7 Security & Auth
- [ ] **JWT** — token validation parameters, signing key handling, lifetime, audience/issuer checks
- [ ] **OAuth 2.0 / OIDC** — flow correctness, scope/claim mapping, token endpoint usage
- [ ] **HTTPS** — TLS enforcement, HSTS, redirect-to-HTTPS, no plaintext fallbacks
- [ ] **OWASP Top 10** — coverage against the current Top 10 categories end to end
- [ ] **Password Hashing** — algorithm and work factor (Argon2 / bcrypt / PBKDF2, salting, iterations)
- [ ] **Refresh Token mechanism** — rotation, reuse detection, revocation, storage
- [ ] **Timing-safe login** — constant-time credential comparison, no user-enumeration oracle
- [ ] **Lockout policy** — failed-attempt throttling / account lockout against brute force
- [ ] **Permission-based authorization** — fine-grained permissions enforced at the handler/aggregate, not only the controller
- [ ] **Warehouse-scoping data isolation** — tenant/warehouse scope enforced in the query, not hidden in the UI
- [ ] **Default seeders** — no weak/known/shipped credentials in seed data
- [ ] **PII / credential log scrubbing** — sensitive fields excluded from logs and traces
- [ ] **Rate limiting** — throttling on public and authentication endpoints
- [ ] **CORS policy** — explicit origins, no permissive wildcard with credentials
- [ ] **Security headers** — CSP, X-Content-Type-Options, X-Frame-Options / frame-ancestors, Referrer-Policy
- [ ] **Managed Identity** — credential-less access to cloud resources vs connection-string secrets
- [ ] **mTLS** — mutual TLS for service-to-service calls
- [ ] **SBOM** — software bill of materials generated in the pipeline
- [ ] **Artifact Signing** — build artifacts / images signed and verified
- [ ] **Threat Modeling** — documented threat model driving the controls above

### §17 Compliance & Data Governance
- [ ] **Password Hashing** — *shared with §7*; assess once, cross-reference
- [ ] **PII Exclusion from Logs** — *shared concern*; PII/credentials never written to logs or traces
- [ ] **Operational Audit Log** — who-did-what-when, completeness, append-only / tamper-evident
- [ ] **Data Classification** — data labelled by sensitivity, controls keyed to the label
- [ ] **Data Retention Policy** — defined lifetimes, purge/anonymization mechanism
- [ ] **Data Residency** — where data physically lives, region constraints honoured

### §16 Multi-Tenancy — isolation surface only ("Belum direncanakan")
- [ ] **Tenant / warehouse isolation** — does *any* query-level isolation exist; is partial/nascent tenancy code present despite "not planned" (silent decision); is row-level security applied on shared tables

---

## Pre-loaded anti-pattern checklist (floor, not ceiling — extend it)

Apply each explicitly: pass / fail / N-A with a file:line evidence pointer. (See `00-common-core.md` §7.) This dimension is **squarely Tier-2 territory**: lead with **OWASP ASVS** and **OWASP Top 10** for the principle, **Microsoft Learn ASP.NET Core security** for the idiomatic .NET execution, and the **WAF Security pillar** for posture. Reach for Tier 1 only where a principle genuinely lands there (e.g. *Idempotent Receiver* for replay-safe audit writes).

### Security & Auth
1. **`TokenValidationParameters` left on defaults** — `ValidateAudience` / `ValidateIssuer` / `ValidateIssuerSigningKey` not all explicitly `true`, `ValidateLifetime` off, or clock skew left at the 5-minute default on short-lived tokens. *(OWASP ASVS V3 Session Management / V7 Tokens; Microsoft Learn — JWT bearer validation.)*
2. **Antiforgery absent on cookie-auth state-changing endpoints** — `POST`/`PUT`/`DELETE` reachable via a cookie identity with no `[ValidateAntiForgeryToken]` / antiforgery middleware. *(OWASP Top 10 A01 Broken Access Control — CSRF; Microsoft Learn — antiforgery in ASP.NET Core / Blazor.)*
3. **DataProtection keys not persisted to shared storage** — keys ring kept in-memory or on local disk, so cookies/antiforgery/tokens break across instances after a restart or scale-out. *(Microsoft Learn — Data Protection key storage & persistence in multi-instance deployments.)*
4. **Mass assignment / overposting** — request binds straight onto the EF entity or a broad model, letting a caller set fields they shouldn't (role, price, ownerId, warehouseId). *(OWASP API Top 10 API6 Mass Assignment.)* **Cross-session note:** overposting is also assessed by session 03 (API Design) — flag, do not double-count; this session owns the **privilege-escalation/isolation** angle.
5. **Authorization enforced only at the controller** — `[Authorize]`/policy on the endpoint but the application handler or aggregate trusts the call blindly, so any other caller of the handler bypasses the check. *(OWASP ASVS V4 Access Control — enforce at the trust boundary that owns the data; OWASP Top 10 A01.)*
6. **Secrets in source or config files** — connection strings, signing keys, API keys, or passwords in `appsettings*.json`, committed `.env`, or code, rather than a secret store / user-secrets / Key Vault. *(OWASP ASVS V6 Stored Cryptography / V14 Config; WAF Security — secrets management.)*
7. **CORS overly permissive** — `AllowAnyOrigin` (or reflected origin) combined with `AllowCredentials`, or a wildcard policy on authenticated APIs. *(OWASP ASVS V14.5; Microsoft Learn — CORS policy configuration.)*
8. **Missing rate limiting on public / auth endpoints** — login, token, refresh, password-reset, and public reads have no throttling, leaving brute-force and resource-exhaustion open. *(OWASP Top 10 A07 Identification & Authentication Failures; Microsoft Learn — ASP.NET Core rate limiting middleware.)*
9. **PII or credentials in logs / traces** — passwords, tokens, full PII, or secrets reach Serilog sinks or OTel spans without redaction/masking. *(OWASP ASVS V7 Error Handling & Logging — never log sensitive data.)* **Cross-session note:** PII-in-logs/traces also lands in session 05 (Observability) — assess the sensitive-data-handling rule here, reconcile with 05.
10. **Weak password hashing** — anything other than a memory-hard / adaptive KDF (Argon2id preferred, then bcrypt or PBKDF2) with a per-user salt and an adequate work factor; home-rolled hashing, plain SHA-256, or too-few PBKDF2 iterations. *(OWASP Password Storage Cheat Sheet; OWASP ASVS V2.4 Credential Storage.)*
11. **Non-timing-safe credential comparison** — login compares password hash or token with `==`/ordinary string compare, and reveals "user not found" vs "wrong password" distinctly, creating timing and enumeration oracles. *(OWASP ASVS V2.2 — constant-time comparison; uniform authentication responses.)*
12. **No account-lockout / brute-force throttling** — unlimited failed login attempts with no lockout, backoff, or CAPTCHA gate. *(OWASP ASVS V2.2.1; OWASP Top 10 A07.)*
13. **Refresh tokens without rotation or reuse detection** — long-lived refresh tokens reused indefinitely, not rotated on use, and a replayed/stolen token is not detected and the family revoked. *(OWASP ASVS V3 Session Management — token rotation & revocation; OAuth 2.0 Security BCP — refresh token rotation.)*
14. **Tenant / warehouse scope enforced in the UI only** — the warehouse filter is a dropdown or client-side predicate, but the query/repository does not constrain by scope, so a crafted request reads another scope's data. *(OWASP Top 10 A01 Broken Access Control — server-side enforcement; insecure direct object reference.)*
15. **No row-level security on shared multi-tenant tables** — tenant/warehouse rows co-mingled in one table with no DB-enforced RLS policy and no global query filter, relying entirely on application code remembering to filter. *(OWASP ASVS V4 Access Control; PostgreSQL Row-Level Security / EF Core global query filters — defence in depth.)*
16. **Default seeders shipping weak or known credentials** — seeded admin/user with a hard-coded or well-known password, no forced rotation on first sign-in. *(OWASP Top 10 A07; OWASP ASVS V2.3 — no shipped default credentials.)*
17. **Connection-string secrets where Managed Identity fits** — cloud DB / storage / Key Vault reached with an embedded secret instead of a workload/managed identity. *(WAF Security — managed identities; OWASP ASVS V6 — credential-less access where available.)*
18. **No mTLS for service-to-service calls** — internal service hops authenticate the user but not the *caller service*, so a foothold inside the network can impersonate any service. *(WAF Security — service-to-service authentication / zero-trust; paraphrase where ASVS is thin.)*
19. **No SBOM / unsigned artifacts** — pipeline emits no software bill of materials and ships unsigned images/binaries, so supply-chain provenance is unverifiable. *(OWASP Top 10 A06 Vulnerable & Outdated Components; SLSA / supply-chain provenance — paraphrase if uncertain.)*
20. **No threat model** — no documented STRIDE/asset-driven analysis, so the chosen controls are unmotivated and gaps are invisible. *(WAF Security — threat modelling; Microsoft SDL threat modelling — paraphrase.)*

### Compliance & Data Governance
1. **PII not excluded from logs** — *shared with Security #9*; verify destructuring policies / `Serilog` masking actually strip PII before sinks, not just by convention. *(OWASP ASVS V7.1 — no sensitive data in logs.)*
2. **Audit log incomplete** — security-relevant actions (auth, permission change, data mutation, export) are not all captured with actor, action, target, timestamp, and outcome. *(OWASP ASVS V7.2 — log security-relevant events; WAF Security — auditability.)*
3. **Audit log not append-only / tamper-evident** — audit rows are `UPDATE`/`DELETE`-able through the same path as business data, or the writer can be replayed to forge/duplicate entries with no hash chain or write-once store. *(EIP — Idempotent Receiver for replay-safe audit writes; WAF Security — immutable audit trail; paraphrase tamper-evidence.)*
4. **No data classification** — data is not labelled by sensitivity, so there is no basis for differentiated handling, masking, or retention. *(ISO/IEC 25010 as the quality frame; WAF Security — data classification; paraphrase the principle.)*
5. **No data-retention policy / purge mechanism** — records (including PII and audit) live forever with no defined lifetime, anonymization, or purge job. *(WAF Security / Operational Excellence — data lifecycle; paraphrase — Tier 1 silent here.)*
6. **Data-residency constraints unaddressed** — no statement or control over which region stores/processes data; tri-mode cloud could place data anywhere. *(WAF Security — data residency / sovereignty; paraphrase.)*
7. **Password storage assessed twice / inconsistently** — §7 and §17 both list it; confirm a *single* hashing implementation, not two divergent ones. *(OWASP ASVS V2.4 — single, consistent credential-storage policy.)*
8. **Audit trail missing correlation to the request** — audit entries can't be tied back to a request/trace, so an incident can't be reconstructed end to end. **Cross-session note:** correlation-ID propagation is owned by session 05 (Observability); here assess only that the audit record *carries* enough identity to be reconstructable.

---

## Mini flow-traces for this session (Method 3, scoped)

Trace at the **security/trust-boundary altitude**, not for business correctness. Mark every discontinuity ("this is where the check is missing") as a `SEC` finding.

- **Authenticated, state-changing request (authz + isolation + audit):** authenticated request → authorization decision **at the application handler / aggregate** (not merely the controller attribute) → **tenant / warehouse data scoping applied in the query / repository** (does a crafted request for another scope's id get filtered, or returned?) → **audit-log write** (is the mutation recorded with actor/target/outcome, and is that write append-only?). *Watch:* a handler that trusts the controller; a repository that forgets the scope filter; a mutation that completes with no audit entry.
- **Login flow (credential handling):** submit credentials → user lookup → **password verification with a memory-hard/adaptive KDF and constant-time comparison** → **uniform response** (no user-enumeration timing/message oracle) → **failed-attempt accounting and lockout** → token/refresh issuance. *Watch:* `==` comparison, distinct "no such user" vs "bad password" responses, no lockout counter, refresh token issued without rotation metadata.

---

## Primary anchors for this session

Lead with the **Tier-2 security stack** from `00-common-core.md` §8, because this dimension is squarely Tier-2 territory per the citation-preference rule:
- **OWASP ASVS** and **OWASP Top 10** — the **primary** source for every auth, access-control, crypto, logging, and validation verdict; cite the **specific verification requirement / category** (e.g. ASVS V2.4 Credential Storage, V4 Access Control, Top 10 A01 Broken Access Control), never just "OWASP".
- **Microsoft Learn — ASP.NET Core security** — for idiomatic .NET *execution*: JWT bearer validation, antiforgery, Data Protection key persistence, CORS, rate-limiting middleware, security headers, ASP.NET Core Identity hashing.
- **Azure Well-Architected Framework — Security pillar** — for posture: secrets management, managed identity, service-to-service auth, auditability, data classification/residency.

Use **Tier 1 only where genuinely applicable** — e.g. **Hohpe & Woolf — *Idempotent Receiver*** for replay-safe audit/event writes. Where no named source states a principle in the form claimed (tamper-evidence, SBOM/provenance, mTLS specifics, retention/residency), **paraphrase without invoking authority** — honesty beats appeal-to-authority.

**Reference implementations to compare against:** **eShop** (ASP.NET Core Identity + IdentityServer/Duende-style token issuance, authorization policies) and the **Jason Taylor / Ardalis Clean Architecture template** (authorization at the application layer, current-user abstraction, audit interceptor). For each big-ticket control present in WMS (token validation, permission model, audit logging, scope isolation), name the reference equivalent and label the difference **intentional / drift / missing**.

---

## ADR touchpoints to verify (Method 1)

Open every ADR touching: **authentication** (JWT / OAuth / OIDC choice, token lifetimes), **authorization model** (permission-based vs role-based, where it's enforced), **data isolation / multi-tenancy** (warehouse-scoping, row-level security — note that §16 says *not planned*, so any decision here may be **silent**), **secrets management** (user-secrets / Key Vault / managed identity), **audit logging** (what's captured, immutability), and **data governance** (classification, retention, residency). For each: *claimed decision | code reality | faithful / drifted / silent / missing | evidence (file:line)*.

**Surface silent decisions aggressively here** — security choices are often made in code with no ADR: a chosen hashing algorithm and work factor, a token lifetime, a CORS policy, a global query filter for scoping, a decision to log (or not redact) a field. **Any tenant-isolation code existing despite "Belum direncanakan" is a silent decision** and a finding. Record all of these in the partial's **ADR touchpoints** block for session 99 to consolidate into the ADR drift matrix.

---

## Output

Write the partial report following the **partial-report skeleton in `00-common-core.md` §11**, covering §7 Security & Auth, §17 Compliance & Data Governance, and the §16 isolation surface, with all findings under the `SEC` prefix and a **scorecard-contribution row per dimension** (Security & Auth · Compliance & Data Governance · Multi-Tenancy-isolation). Assess each **shared concern (password hashing, PII-in-logs) once** and cross-reference between §7 and §17 rather than filing it twice. Then stop — the report is the work.
