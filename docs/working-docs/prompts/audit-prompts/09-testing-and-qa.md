# Session 09 — Testing & QA Architecture

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **dimension-specific scrutiny** for this session.

- **Session code / partial filename:** `09-testing-and-qa`
- **Finding ID prefix:** `QA` (e.g. `QA-001`)
- **Checklist sections covered:** §14 Testing Strategy — assessed as the **Testing/QA architecture cross-lens** (from `tech-checklist-blank.md`)
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-09-testing-and-qa.md` (WMS repo root)

This is the **test-architecture** session. Testing/QA is one of the two named cross-lenses, and it is audited here as an *architecture in its own right* — not folded into Code Quality (session 01) as a code smell. The questions are structural: is the **test pyramid** the right shape (a wide base of fast unit tests, a thin cap of slow E2E), do tests assert on **observable behaviour** rather than implementation detail, are they **deterministic** (no wall-clock, randomness, ordering, or shared-state dependence), and is coverage measured by **behaviour/scenarios** rather than line percentage? A test suite that is green but tests the wrong things at the wrong altitude is a maturity trap, and this session exists to catch it. **Method 2 (audit by omission)** leads here — the most damaging test defects are the tests that *should exist and don't* (component, contract, architecture-fitness, production tests), which no file invites you to "open and check."

> **Tier-1 gap (state it explicitly in the report).** The owner's shelf has **no** Meszaros *xUnit Test Patterns* and **no** Freeman & Pryce *Growing Object-Oriented Software, Guided by Tests* — the two canonical texts for test smells and behaviour-driven test design. For test-smell findings (over-mocking, mystery guest, eager test, fragile/non-deterministic test, slow test), **name the smell and paraphrase the principle without invoking authority**, and note that adding one of these books would close the loop. Lead instead with Hombergs (testing per layer in a hexagonal codebase), Martin (architecture/fitness-function tests as Level-5 governance), Newman (consumer-driven contract testing at boundaries), and Microsoft Learn (testing in .NET, Testcontainers, `WebApplicationFactory`) / Pact docs for contract testing.

---

## Scope — checklist items to verdict

### §14 Testing Strategy
- [ ] **Unit Testing** — fast, isolated, behaviour-asserting tests on domain/application logic
- [ ] **Architecture / fitness functions** — tests enforcing dependency direction, layering, naming conventions
- [ ] **Integration Testing** — real adapter/persistence boundaries exercised against doubles, not prod services
- [ ] **E2E Testing** — full-stack flows through the deployed surface; the thin cap of the pyramid
- [ ] **Test Pyramid awareness** — deliberate altitude distribution, not an inverted/ice-cream-cone shape
- [ ] **Component Testing** — in-process API tests via `WebApplicationFactory` (the missing middle)
- [ ] **Contract Testing (Pact)** — consumer-driven contracts at service / integration-event boundaries
- [ ] **Production Testing (Canary / Shadow)** — canary analysis / shadow traffic validation in prod

---

## Pre-loaded anti-pattern checklist (floor, not ceiling — extend it)

Apply each explicitly: pass / fail / N-A with a file:line evidence pointer. (See `00-common-core.md` §7.)

### Test pyramid & altitude
1. **Inverted pyramid / ice-cream cone** — many slow E2E or broad integration tests, few fast unit tests; the base is the wrong layer, so the suite is slow and flaky and gives weak localisation. *(Newman, Building Microservices — test-pyramid / test-scope guidance; paraphrase the shape rule.)*
2. **Tests at the wrong altitude** — domain invariants verified only through HTTP/E2E paths that boot the whole stack, instead of as unit tests on the aggregate/value object; or trivial getters tested at integration altitude. *(Hombergs, Get Your Hands Dirty on Clean Architecture — testing per layer: each layer has its own appropriate test type.)*
3. **No component / in-process API tests** — the "missing middle" between unit and full E2E is absent; there is no in-memory host (`WebApplicationFactory<TEntryPoint>`) exercising routing + filters + handler + serialization without network or external infra. *(Microsoft Learn — integration tests in ASP.NET Core with `WebApplicationFactory`.)*
4. **Coverage measured by line %, not behaviour** — a coverage gate on line/branch percentage with no notion of scenario or behavioural coverage; high % masks untested edge cases and error paths. *(Paraphrase — coverage measures executed lines, not asserted behaviour; cite the test-smell library gap.)*

### Behaviour vs implementation
5. **Testing implementation, not behaviour (over-mocking)** — asserting on `mock.Verify(...)` / interaction counts rather than the observable outcome; mocking types the unit owns or value objects; tests that break on safe refactors. *(Test smell — over-specified / mockist tests; paraphrase, note the Freeman & Pryce / Meszaros gap.)*
6. **Mystery guest / hidden fixture coupling** — tests depend on out-of-view shared seed data, external files, or a pre-populated database whose contents aren't visible in the test. *(Test smell — Mystery Guest; paraphrase, note the Meszaros gap.)*
7. **No object mothers / test data builders** — fixture setup is copy-pasted and brittle; constructing an aggregate for a test requires reproducing infrastructure concerns, so setup rot spreads (shotgun surgery on every model change). *(Test smell — fragile fixture; paraphrase. Cross-link Code Quality session 01 for the duplication smell.)*
8. **Assertions too coarse or absent** — tests that assert "no exception thrown" / only on a status code while ignoring the response body or persisted state; happy-path-only with no error/edge branches. *(Paraphrase — a test with weak assertions is a false-confidence test.)*

### Determinism
9. **Non-deterministic tests — wall-clock / randomness** — logic reads `DateTime.Now`/`DateTimeOffset.UtcNow` or `Random` directly instead of an injected `TimeProvider` / clock abstraction / seeded source, so tests flake near boundaries. *(Microsoft Learn — `TimeProvider` for testable time; paraphrase the determinism principle.)*
10. **Order-dependent or shared-mutable-state tests** — tests that pass only in a given execution order, share a static/singleton fixture they mutate, or leak state between cases via a non-reset database/container. *(Test smell — interacting tests / shared fixture; paraphrase, note the Meszaros gap.)*
11. **Flaky tests tolerated or quarantined forever** — `[Skip]`/`[Trait("flaky")]`/retry-until-green attributes used as a permanent crutch; a flaky test left in the suite erodes trust in the whole gate. *(Paraphrase — a quarantined-forever test is dead test code that hides a real defect.)*

### Integration & contract boundaries
12. **Integration tests hitting real external services** — tests calling a live database/broker/cloud API instead of **Testcontainers** (ephemeral real engines) or in-memory doubles, making them slow, networked, and non-hermetic. *(Microsoft Learn — Testcontainers for .NET integration testing; Hombergs — testing adapters at the boundary.)*
13. **No contract tests at service / integration-event boundaries** — producers and consumers of integration events (Outbox→broker→Inbox) or HTTP/gRPC contracts are verified only by hope; a schema change can silently break a consumer with no failing test. *(Newman, Building Microservices — consumer-driven contracts; Pact documentation for the mechanics.)*
14. **In-memory provider substituted for the real database in integration tests** — using the EF Core in-memory provider (or SQLite) as a stand-in for PostgreSQL, so provider-specific behaviour (constraints, concurrency, JSON, migrations) goes untested. *(Microsoft Learn — EF Core testing guidance: test against the real database engine via Testcontainers, not the in-memory provider.)*

### Governance & production
15. **No architecture / fitness-function tests** — dependency direction, layering, and naming are not enforced by NetArchTest/ArchUnitNET tests, so structure can silently rot; the architecture has no executable guardrail. *(Martin, Clean Architecture — the Dependency Rule made enforceable; maps to Level-5 governance in the maturity rubric. **Cross-link session 01 §6.**)*
16. **Tests not run in CI as a merge gate** — the suite exists but isn't wired as a required check on PR/merge, or slow tiers are excluded from the gate without a documented strategy. *(Paraphrase — an unenforced test suite is documentation, not a gate. **Cross-link session 07 CI gate.**)*
17. **No production testing (canary analysis / shadow traffic)** — no automated canary analysis or shadow/mirrored-traffic validation; the only "test in prod" is the user noticing. *(Paraphrase / WAF & deployment-strategy guidance; legitimate to mark N-A for a sandbox but record the omission.)*

---

## Mini flow-traces for this session (Method 3, scoped)

Pick **ONE representative feature** (ideally a write-heavy command that also raises a domain/integration event — so the trace can reach contract boundaries) and trace its **test coverage across the whole pyramid**, not its runtime behaviour:

- **Unit altitude** — are the aggregate invariants, value-object validation, and domain-event emission asserted in fast isolated tests, asserting on *outcomes* (state, returned `Result`, emitted events) rather than on mocked interactions?
- **Component altitude** — is there an in-process API test (`WebApplicationFactory`) covering the endpoint → handler → serialization round trip, including the *failure* branches (validation 400, not-found 404, conflict 409)?
- **Integration altitude** — is the persistence adapter / Outbox writer exercised against a **real PostgreSQL via Testcontainers** (not in-memory), and is the consumer/projection side tested against a real broker double?
- **Contract altitude** — for the integration event this feature emits, is there a consumer-driven contract test pinning the schema both sides agree on?
- **E2E altitude** — is the same scenario *also* duplicated as a slow full-stack test (pyramid inversion), or is E2E reserved for a thin smoke of the critical path?

Mark every **behavioural gap** (a behaviour asserted at *no* altitude) and every **wrong-altitude** test (a behaviour asserted only where it's slow/fragile when a cheaper altitude would do) as a `QA` finding. The deliverable of the trace is a per-altitude map of *what is tested, where, and what is missing*.

---

## Primary anchors for this session

Lead with these from `00-common-core.md` §8 (and the noted Tier-1 gap):

- **Tom Hombergs — *Get Your Hands Dirty on Clean Architecture*** — testing per layer; which test type belongs at the unit vs integration boundary in a hexagonal codebase; testing adapters at the port. *Lead for altitude and per-layer test-type findings.*
- **Robert C. Martin — *Clean Architecture*** — architecture/fitness-function tests as the executable enforcement of the Dependency Rule; Level-5 governance that keeps structure true. **Cross-link session 01 §6.**
- **Sam Newman — *Building Microservices*** — test-pyramid shape and scope; **consumer-driven contract testing** at service / integration boundaries.
- **Tier-2 leads (dimension is squarely Tier-2 here):** **Microsoft Learn** — testing in .NET, `WebApplicationFactory` component tests, **Testcontainers** for hermetic integration, `TimeProvider` for deterministic time, EF Core testing guidance (real engine over in-memory). **Pact documentation** — contract-testing mechanics.
- **Tier-1 gap, paraphrase:** test smells (over-mocking/mockist tests, Mystery Guest, eager test, fragile fixture, interacting tests, slow test) — **no Meszaros *xUnit Test Patterns*, no Freeman & Pryce *GOOS* on the shelf.** Name the smell, paraphrase, note the gap.

**Reference implementations to compare against** (label each delta *intentional / drift / missing*): **eShop** (component tests via `WebApplicationFactory`, functional test projects), **Kamil Grzybek — Modular Monolith with DDD** (architecture tests + integration tests per module), **Jason Taylor / Ardalis Clean Architecture template** (test project layout, fixture/builder conventions, architecture-test project), **MassTransit test harness** (in-memory harness for consumer/saga testing). For each big-ticket testing pattern in WMS (unit, component, integration-with-Testcontainers, contract, architecture-fitness), name the reference equivalent and identify the substantive difference.

---

## ADR touchpoints to verify (Method 1)

Open every ADR touching: **testing strategy**, **test-pyramid policy** (which altitudes, what target distribution), **contract-testing approach** (Pact / schema verification at boundaries), and **CI quality gates** (which test tiers gate merge, coverage thresholds, flaky-test policy). For each: *claimed decision | code reality | faithful / drifted / silent / missing | evidence (file:line)*. Verify the *claimed* pyramid against the *actual* test-project counts and run times — a stated "pyramid" that is inverted in practice is **drift**, default High. Surface **silent** testing decisions too: a coverage threshold buried in a `.csproj`/`coverlet` config, an in-memory-provider shortcut, a `TimeProvider`-vs-`DateTime.Now` choice, or a Testcontainers-vs-live-service decision made in code with no ADR. Record all of these in the partial's **ADR touchpoints** block for session 99 to consolidate.

---

## Output

Write the partial report following the **partial-report skeleton in `00-common-core.md` §11**, covering §14 Testing Strategy as the **Testing/QA architecture cross-lens**, with all findings under the `QA` prefix and a scorecard-contribution row for this dimension (R/A/G + maturity 1–5 + one-line summary). Then stop — the report is the work.

**Cross-session note:** *architecture / fitness-function tests* overlap **session 01 §6 (Code Quality)** — assess the test *architecture* (do they exist, do they enforce the right rules, are they wired as a gate) here, and defer the per-rule SOLID/dependency-direction verdicts to session 01; flag the overlap so synthesis reconciles rather than double-counts. The *CI test-gate* (suite wired as a required merge check) overlaps **session 07 (DevOps & CI/CD)** — assess test-suite adequacy and gating *intent* here, defer the pipeline-mechanics verdict to session 07.
