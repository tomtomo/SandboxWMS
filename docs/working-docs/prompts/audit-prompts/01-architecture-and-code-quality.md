# Session 01 — Application Architecture & Code Quality

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **dimension-specific scrutiny** for this session.

- **Session code / partial filename:** `01-architecture-and-code-quality`
- **Finding ID prefix:** `ARC` (e.g. `ARC-001`)
- **Checklist sections covered:** §1 Application Architecture · §6 Code Quality (from `tech-checklist-blank.md`)
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-01-architecture-and-code-quality.md` (WMS repo root)

This is the **structural-integrity** session: how the system is *shaped* (DDD, Clean Architecture, CQRS, vertical slice) and how *idiomatic* the code that fills that shape is (SOLID, GoF, analyzers, fitness functions). It is the natural home of **Method 1 (decision-centric review)** because architecture is where decisions drift hardest and most expensively. Record ADR touchpoints carefully — session 99 builds the ADR drift matrix from them.

---

## Scope — checklist items to verdict

### §1 Application Architecture
- [ ] **DDD Tactical** — aggregates, entities, value objects, domain events, repositories, factories
- [ ] **DDD Strategic** — bounded contexts, context mapping, ubiquitous language, anti-corruption layer
- [ ] **Clean Architecture & CQRS** — dependency rule, layer concentricity, command/query segregation
- [ ] **Vertical Slice** — feature cohesion, slice independence
- [ ] **Domain Service** — domain logic that legitimately doesn't belong on an entity/VO
- [ ] **Result Pattern** — explicit success/failure modelling vs exceptions-for-control-flow
- [ ] **Stateful boundary** — where, and why, request/session/tenant state lives

### §6 Code Quality
- [ ] **Roslyn Analyzers** — enabled, warnings-as-errors, nullable reference types
- [ ] **SOLID principles** — at module and code altitude
- [ ] **Design Patterns (GoF)** — applied correctly, not cargo-culted
- [ ] **Fitness Functions in CI** — architecture tests enforcing the rules above

---

## Pre-loaded anti-pattern checklist (floor, not ceiling — extend it)

Apply each explicitly: pass / fail / N-A with a file:line evidence pointer. (See `00-common-core.md` §7.)

### Application Architecture
1. **Anemic domain model** — entities are data bags; business rules live in services/handlers. *(Vernon, Anemic Domain Model anti-pattern; Evans, model-driven design.)*
2. **Aggregate boundary leaks** — repositories querying *across* aggregate roots; one aggregate holding a direct object reference to another instead of by ID; a transaction mutating two aggregates. *(Vernon, Aggregate Design rules of thumb — reference by identity, one aggregate per transaction.)*
3. **Dependency-rule violation** — domain/application code referencing infrastructure (EF Core types, `DbContext`, ASP.NET, provider SDKs) or compiling against outer-ring concretions. *(Martin, Clean Architecture — the Dependency Rule.)*
4. **"Renamed layers" only** — Clean Architecture exists in folder names but the domain depends on `Microsoft.EntityFrameworkCore`, attributes, or framework types. *(Hombergs — does the code reflect the theory or just rename the layers?)*
5. **CQRS not actually segregated** — commands and queries share one model/handler; queries are routed through aggregates/the write model; or it's "CQRS" with a single store but no behavioural separation. *(Distinguish from event sourcing; cite idiomatic CQRS — Microsoft .NET architecture guidance, or paraphrase.)*
6. **Vertical slice leaking horizontal coupling** — slices reach into each other or share mutable infrastructure/state rather than being independently changeable. *(Newman, module boundaries; vertical-slice cohesion.)*
7. **Result pattern applied inconsistently** — some paths return `Result<T>`, others throw for *expected* business outcomes; exceptions used as control flow. *(Paraphrase / Microsoft guidance; flag the inconsistency as the finding.)*
8. **Domain Service misuse** — logic that belongs on an aggregate/entity is pushed into a stateless service, hollowing the model; or "domain services" that are really application orchestration. *(Evans, Services — thin and stateless, only for operations that aren't a natural responsibility of an entity/VO.)*
9. **Bounded-context bleed** — shared EF entities or a shared kernel spanning contexts; integration without an anti-corruption layer; ubiquitous language diverging from code names. *(Evans, Bounded Context, Context Mapping, ACL.)*
10. **Primitive obsession at the domain core** — identifiers and value concepts as raw `string`/`Guid`/`decimal` instead of value objects, so invariants live nowhere. *(Vernon, Value Objects.)*
11. **Stateful boundary smell** — singletons or static mutable state holding per-request/per-tenant data; scoped state captured by a singleton. *(Cross-link to session 04 if it touches tenant isolation.)*

### Code Quality
1. **SRP violation** — god handlers/classes mixing orchestration + validation + mapping + persistence. *(Martin, APPP — SRP.)*
2. **OCP violation** — `switch`/`if` ladders on a type code where polymorphism/Strategy belongs. *(Martin, APPP — OCP; Freeman & Robson — Strategy.)*
3. **LSP violation** — subtypes that throw `NotImplementedException`, strengthen preconditions, or weaken postconditions. *(Martin, APPP — LSP.)*
4. **ISP violation** — fat interfaces forcing implementers to stub unused members. *(Martin, APPP — ISP.)*
5. **DIP violation** — high-level policy depending on low-level detail; `new`-ing infrastructure inside domain/application. *(Martin, APPP — DIP.)*
6. **GoF pattern misuse** — Factory used as Service Locator; Strategy vs Template Method confusion; Observer wired so subscribers can't be reasoned about; Singleton hiding global state. *(Freeman & Robson — the specific pattern.)*
7. **Package/component-principle violations** — cyclic dependencies between modules; instability flowing the wrong way; common-closure/common-reuse broken. *(Martin, APPP — REP/CCP/CRP, ADP/SDP/SAP.)*
8. **Analyzer hygiene gaps** — analyzers or warnings-as-errors disabled; nullable reference types off; `#pragma warning disable` or `<NoWarn>` used to silence rather than fix. *(Microsoft .NET guidance; paraphrase.)*
9. **No fitness functions / architecture tests** — dependency direction, layering, and naming conventions are not enforced by tests (NetArchTest / ArchUnitNET), so the architecture can silently rot. *(Maps to Level-5 governance in the maturity rubric.)*
10. **Code smells** — long methods, feature envy, shotgun surgery, duplicated logic. *(No Fowler* Refactoring *on the shelf — name the smell and paraphrase; note the library gap.)*

---

## Mini flow-traces for this session (Method 3, scoped)

Trace at the **architecture altitude**, not for business correctness:
- **One command, edge to core:** controller/endpoint → MediatR/application handler → domain aggregate → repository port. *Watch:* does the dependency direction hold inward the whole way? Where does an infrastructure type (EF entity, `DbContext`, provider type) leak inward? Is the handler doing domain work that belongs on the aggregate?
- **One query:** does it bypass the write model / aggregates (idiomatic CQRS read side), or does it load aggregates just to project them?

Mark every discontinuity ("this is where the layering breaks") as an `ARC` finding.

---

## Primary anchors for this session

Lead with these from `00-common-core.md` §8: **Evans** (strategic + foundational tactical DDD) · **Vernon** (tactical execution in .NET-shaped code) · **Martin, *Clean Architecture*** (dependency rule, layering) · **Hombergs** (Clean Architecture *execution*) · **Martin, *APPP*** (SOLID, package principles) · **Freeman & Robson** (GoF) · **Newman** (module boundaries / decomposition). Reference implementations to compare structure against: **eShop**, **Kamil Grzybek — Modular Monolith with DDD**, **Jason Taylor / Ardalis Clean Architecture template**.

---

## ADR touchpoints to verify (Method 1)

Open every ADR touching: architecture style (modular monolith vs microservices), layering / dependency rule, CQRS, vertical slice, Result pattern, aggregate/bounded-context design, error-handling strategy. For each: *claimed decision | code reality | faithful / drifted / silent / missing | evidence (file:line)*. Also surface **silent** architectural decisions — significant structural choices in code with no ADR. Record all of these in the partial's **ADR touchpoints** block for session 99 to consolidate.

---

## Output

Write the partial report following the **partial-report skeleton in `00-common-core.md` §11**, covering §1 and §6, with all findings under the `ARC` prefix and a scorecard-contribution row for each of the two dimensions. Then stop — the report is the work.
