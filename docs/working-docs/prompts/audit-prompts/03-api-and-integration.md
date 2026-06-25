# Session 03 — API Design & Communication & Integration

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **dimension-specific scrutiny** for this session.

- **Session code / partial filename:** `03-api-and-integration`
- **Finding ID prefix:** `API` (e.g. `API-001`)
- **Checklist sections covered:** §3 API Design · §4 Communication & Integration (from `tech-checklist-blank.md`)
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-03-api-and-integration.md` (WMS repo root)

This is the **boundaries-and-contracts** session: the system's two outward-facing edges. The synchronous API surface (REST, gRPC, BFF, OpenAPI/AsyncAPI) and the asynchronous messaging/integration surface (domain events, Transactional Outbox, Idempotent Consumer, DLQ, Saga, schema registry) are clustered together because they tell **one story** — how WMS exposes and consumes contracts across its edges. They share the same three load-bearing concerns: **contract versioning** (a request DTO and an integration-event schema both have to evolve without breaking callers), **idempotency** (a retried POST and a redelivered message both need the same dedup discipline), and **coupling** (the choice of sync-vs-async, choreography-vs-orchestration, and how much domain leaks across the boundary). Auditing them apart would split those concerns; auditing them together keeps the contract story whole.

---

## Scope — checklist items to verdict

### §3 API Design
- [ ] **REST API** — verbs/status-code semantics, resource modelling, no RPC-over-HTTP smell
- [ ] **OpenAPI / Swagger** — spec presence, accuracy vs implementation, generated vs hand-maintained
- [ ] **Pagination** — present at all; offset vs keyset where each belongs
- [ ] **BFF Pattern** — backend-for-frontend responsibilities vs leaking domain to the client
- [ ] **Idempotency** — idempotency keys on mutating (POST/PUT/PATCH) endpoints
- [ ] **gRPC internal** — internal service-to-service contract, proto ownership, status-code mapping
- [ ] **AsyncAPI** — the async/event surface is documented as a contract, not just the sync one

### §4 Communication & Integration
- [ ] **Request-Response (sync)** — synchronous call sites; where sync coupling is chosen deliberately vs by default
- [ ] **In-Process Domain Events** — intra-aggregate/intra-context eventing, dispatch timing vs transaction boundary
- [ ] **Transactional Outbox Pattern** — at-least-once write of message + state in one transaction; dispatcher idempotency
- [ ] **Task Queue** — background work hand-off, enqueue semantics, poison-message handling
- [ ] **Pub/Sub Messaging activation** — broker wiring, topic/channel modelling, publisher/subscriber decoupling
- [ ] **Schema Registry + Event Versioning** — backward/forward compatibility, contract evolution governance
- [ ] **Idempotent Consumer Pattern** — Inbox / dedup store on the receive side
- [ ] **Dead Letter Queue (DLQ)** — poison routing plus an inspection/replay workflow, not just a graveyard
- [ ] **Saga (Orchestration)** — multi-step process coordination, compensation, timeouts
- [ ] **Delayed Task Queue** — scheduled/deferred dispatch, retry semantics, visibility timeouts

---

## Pre-loaded anti-pattern checklist (floor, not ceiling — extend it)

Apply each explicitly: pass / fail / N-A with a file:line evidence pointer. (See `00-common-core.md` §7.)

### API Design
1. **RPC-over-HTTP smell** — verbs ignored (everything `POST /doSomething`), action verbs baked into URIs, a flat tunnel of one method instead of resources. The Richardson Maturity Model stalls at Level 0/1 with no real HTTP-as-application-protocol use. *(Tier 2: Richardson Maturity Model; HATEOAS/uniform-interface constraint — paraphrase if unsure.)*
2. **Wrong status-code semantics** — `200 OK` for created/failed/empty results, `200` wrapping an error body, no `201 Location`, no `409`/`422`/`412` where they belong, `500` for client faults. *(Tier 2: RFC 9110 status-code semantics.)*
3. **Anemic / wrong resource modelling** — resources modelled as verbs or as the database schema, no clear aggregate-rooted resource identity, collection vs item conflated. *(Newman, integration styles & resource granularity; cross-link session 01 aggregate boundaries.)*
4. **Missing idempotency keys on mutating endpoints** — POST/PUT/PATCH with no `Idempotency-Key` (or equivalent) contract, so a client retry after a timeout double-applies the effect. *(Hohpe & Woolf — Idempotent Receiver, applied to the sync edge.)*
5. **Pagination missing, or offset where keyset belongs** — unbounded list endpoints; offset/`Skip-Take` paging over large, mutating sets where keyset (seek) pagination is the correct, stable, index-friendly choice. *(Tier 2: WAF Performance Efficiency / Microsoft REST API guidance; paraphrase the offset-instability argument if unsure.)*
6. **Overposting / mass-assignment on inbound DTOs** — endpoints bind directly to domain entities or fat DTOs, letting a caller set fields they should not (status, owner, prices). *(Tier 2: OWASP API3 Broken Object Property Level Authorization / mass assignment — cross-link session 04 Security.)*
7. **No deliberate API versioning strategy** — no URL/header/media-type versioning, breaking changes shipped silently, or versioning declared but inconsistently applied across endpoints. *(Bellemare, contract evolution applied to the sync surface; Tier 2: Microsoft API versioning guidance.)*
8. **BFF leaking domain** — the BFF passes domain aggregates/EF entities straight to the client, or contains business rules that belong in the application core, so it is a passthrough rather than a client-shaped facade. *(Newman, Backends-for-Frontends — the BFF owns client-shaping, not domain logic.)*
9. **OpenAPI/AsyncAPI spec drift or absence** — no machine-readable contract, or a spec that no longer matches the running endpoints/events (source-of-truth rule: the code wins, the stale spec is the finding). *(Tier 2: OpenAPI & AsyncAPI specifications.)*
10. **gRPC contract hygiene gaps** — proto field numbers reused/renumbered, `required`-style breaking edits, no status-code mapping to gRPC codes, internal proto ownership unclear. *(Tier 2: gRPC / Protobuf docs; cross-reference Bellemare schema-evolution rules.)*
11. **Chatty / over-fetching endpoints** — N calls where one resource composition belongs, or one fat endpoint returning everything; no Content Enricher where a composed view is needed. *(Hohpe & Woolf — Content Enricher; cross-link session 01/02 read-path findings.)*

### Communication & Integration
1. **Transactional Outbox not at-least-once** — message and state written in *separate* transactions (dual-write), so a crash between them loses the message or emits a phantom. *(Hohpe & Woolf — Guaranteed Delivery; Bellemare, the outbox as the at-least-once bridge.)*
2. **Outbox dispatcher not idempotent** — the relay can publish the same row twice (no marking/locking, no dedup on the broker side), and nothing downstream deduplicates it. *(Hohpe & Woolf — Guaranteed Delivery + Idempotent Receiver together.)*
3. **No Inbox / Idempotent Consumer on the receive side** — consumers process every redelivery as new; at-least-once delivery is treated as exactly-once by wishful thinking. *(Hohpe & Woolf — Idempotent Receiver; Bellemare, idempotent consumption.)*
4. **DLQ exists but no inspection/replay workflow** — poison messages land in a dead-letter channel that nobody reads and from which nothing can be requeued, so the DLQ is a silent graveyard. *(Hohpe & Woolf — Dead Letter Channel; the catalog implies an operator path, not a void.)*
5. **Saga compensation / timeout gaps** — a Process Manager with a happy path but no compensating actions for partial failure, or no timeout/deadletter for steps that never complete, leaving processes stuck forever. *(Hohpe & Woolf — Process Manager; Bellemare, orchestration with compensation.)*
6. **Correlation ID not propagated across the async boundary** — the trace/correlation context is dropped when a message crosses the broker, so a saga or integration flow can't be traced edge-to-edge. *(Hohpe & Woolf — Correlation Identifier; cross-link session 05 Observability.)*
7. **No schema registry / event versioning discipline** — integration-event schemas evolve in place with no backward/forward-compatibility rules, breaking existing consumers on the next deploy. *(Bellemare, schema/contract evolution & compatibility; event ownership.)*
8. **Notification-vs-state-transfer confusion** — events that should carry state are thin notifications forcing a callback (chatty, coupled), or thin notifications bloated into full state dumps with no need. The choice is accidental, not designed. *(Bellemare, event-carried state transfer vs notification events.)*
9. **Choreography vs orchestration chosen by accident** — a multi-step cross-context process is wired as ad-hoc choreography where a Process Manager (orchestration) was needed for visibility, or centrally orchestrated where loose choreography fit, with no ADR recording the trade-off. *(Bellemare, choreography vs orchestration; Hohpe & Woolf — Process Manager vs Message Router.)*
10. **Sync coupling where async fits (and vice-versa)** — a synchronous request-response call across a context boundary that introduces temporal coupling and a shared availability fate, where an event/queue belonged — or an async hop bolted onto something that needed an immediate consistent answer. *(Newman, integration styles & coupling; Hohpe & Woolf — Message Channel choice.)*
11. **Task-queue / delayed-queue poison handling missing** — background and delayed tasks have no retry-with-backoff, no max-attempts, no poison-message routing, so one bad payload either spins forever or is silently dropped. *(Hohpe & Woolf — Dead Letter Channel + Guaranteed Delivery; cross-link session 05 retry safety.)*
12. **In-process domain events dispatched at the wrong boundary** — domain events raised inside an aggregate are dispatched *before* the transaction commits (so a rollback still fires side effects) or routed through the outbox when they're purely intra-process, blurring the in-process/integration distinction. *(Vernon, domain events & dispatch timing; Bellemare, internal vs integration events.)*
13. **No Message Router / Content-based routing where fan-out is implicit** — routing decisions hard-coded into producers instead of a routing component, so adding a consumer means editing the publisher. *(Hohpe & Woolf — Message Router.)*

---

## Mini flow-traces for this session (Method 3, scoped)

Trace at the **contract / integration altitude**, not for business correctness. Mark every discontinuity ("this is where the contract or delivery guarantee breaks down") as an `API` finding.

- **One cross-boundary integration event (the at-least-once spine):** command → application handler → **Outbox** (message + state in one transaction) → dispatcher/relay → broker (Pub/Sub) → consumer → **Inbox / Idempotent Receiver** → projection/read model. *Watch:* is the outbox write truly in the same transaction as the state change (no dual-write)? Is the dispatcher idempotent and at-least-once? Does the consumer deduplicate redeliveries, or assume exactly-once? Does the correlation ID survive the broker hop? Where does the event schema's version live, and what happens to an old-shaped message?
- **One saga happy path plus one compensation path:** the Process Manager from first command through each step to completion — then force a mid-saga failure and follow the **compensation** branch. *Watch:* is every state-changing step paired with a compensating action? Is there a timeout / dead-letter for a step that never replies? Does a redelivered step command re-execute (no idempotency) and corrupt saga state? Is the saga's state machine explicit, or implicit and unrecoverable?

---

## Primary anchors for this session

Lead with these from `00-common-core.md` §8:

- **Gregor Hohpe & Bobby Woolf — *Enterprise Integration Patterns*** — the canonical catalog for the async surface. Cite pattern names **directly**: **Idempotent Receiver**, **Dead Letter Channel**, **Guaranteed Delivery**, **Message Channel**, **Process Manager**, **Message Router**, **Content Enricher**, **Correlation Identifier**. *Lead anchor for Outbox/Inbox/DLQ/Saga/routing/transformation findings.*
- **Adam Bellemare — *Building Event-Driven Microservices*** — schema/contract evolution, event ownership, choreography vs orchestration, event-carried state transfer vs notification events, idempotent consumption. *Lead anchor for event-versioning, integration-vs-internal-event, and choreography/orchestration findings.*
- **Sam Newman — *Building Microservices*** — integration styles, coupling (temporal/availability), Backends-for-Frontends, resource granularity. *Lead anchor for sync-vs-async coupling and BFF-boundary findings — fully applicable to a modular monolith.*

For the **synchronous surface**, Tier 1 doesn't cover REST mechanics — use Tier 2 per the citation preference rule: **Richardson Maturity Model** (REST maturity), **OpenAPI** & **AsyncAPI** specifications (machine-readable contracts), **gRPC / Protobuf docs** (internal contracts), **RFC 9110** (HTTP semantics & status codes), and **Microsoft REST API / API-versioning guidance**. **Reference implementations to compare against:** **eShop integration events** (outbox → event-bus → idempotent handler), and **MassTransit / Wolverine samples** (saga state machines, inbox/outbox, DLQ + redelivery). For each big-ticket pattern (Outbox, Inbox, Saga, DLQ, event versioning), name the reference equivalent and label WMS's version **intentional / drift / missing**.

---

## ADR touchpoints to verify (Method 1)

Open every ADR touching: **API style** (REST vs gRPC vs BFF, and where each is used), **messaging transport** (the broker/Pub/Sub choice), **Transactional Outbox / Inbox** (delivery guarantee and dedup strategy), **Saga orchestration vs choreography**, **event schema / versioning** (compatibility policy, schema registry), and **idempotency strategy** (sync keys and consumer dedup as one decision or two). For each: *claimed decision | code reality | faithful / drifted / silent / missing | evidence (file:line)*.

Surface **silent** decisions especially — the integration edge is where they hide: an implicit "we assume exactly-once," a choreography that emerged because no one chose orchestration, a versioning convention that lives only in a serializer config, a DLQ with no documented replay path. Record every touchpoint in the partial's **ADR touchpoints** block for session 99 to consolidate into the ADR drift matrix.

---

## Output

Write the partial report following the **partial-report skeleton in `00-common-core.md` §11**, covering §3 and §4, with all findings under the `API` prefix and a **scorecard-contribution row for each of the two dimensions** (API Design; Communication & Integration). Then stop — the report is the work.

**Cross-session note:** *idempotency* is shared surface — the sync-endpoint side (mutating endpoints, overposting) overlaps **session 04 (Security & Auth)**, and the retry-safety side (outbox/consumer dedup under retries) overlaps **session 05 (Resilience/Observability)**; *correlation-ID propagation across the async boundary* overlaps **session 05 (Observability)**; and *DLQ inspection/replay* overlaps **session 05 (Resilience)**. Audit these from the *contracts-and-delivery-guarantee* angle here, flag the overlap in the partial's **Cross-session notes** block, and let session 99 reconcile rather than double-count.
