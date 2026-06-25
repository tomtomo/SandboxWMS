# Session 10 — End-to-End Flow Tracing

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **flow-specific scrutiny** for this session.

- **Session code / partial filename:** `10-flow-tracing`
- **Finding ID prefix:** `FLOW` (e.g. `FLOW-001`)
- **Checklist sections covered:** none — this session is **cross-cutting** by construction. It owns Method 3 at full scale (`00-common-core.md` §6) rather than any single dimension from `tech-checklist-blank.md`.
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-10-flow-tracing.md` (WMS repo root)

This is the **integration-integrity** session. Every other session (01–09) reviews the system *by dimension* — and by construction a per-dimension reviewer stands at one altitude over one code-surface and cannot see what happens *between* the hops. A command can be validated correctly, the aggregate can be modelled correctly, the outbox can be wired correctly, the consumer can be idempotent — and the *flow* can still be broken at the seam where one concern is silently dropped on the way to the next. This session owns the **full end-to-end spine traces** that catch exactly those seam defects: correlation context lost at the messaging boundary, a transaction boundary drawn one hop too wide, an idempotency guarantee that exists on the producer but not the consumer, a read model that can serve a write that was never committed.

It **benefits from running after sessions 01–03** (you can reuse their architecture, persistence, and API maps, and reconcile against findings they already recorded) **but it can run cold** — the spine is discoverable from the entry points alone, and running cold is in some ways *cleaner*, since you trace what the code actually does rather than what a prior session reported. Either way, the flow is the unit of work here, not the dimension.

---

## Flows to trace

Trace **exactly three** representative end-to-end flows. For each, **pick the single most representative real instance in WMS** — do not invent a flow, and do not trace a toy/sample path when a load-bearing one exists. Choose the instance that exercises the most of the spine and is the most architecturally central (a core write command, a hot read, a real cross-context interaction). Name the concrete instance you picked at the top of each trace, and say in one line *why* it is the most representative.

Trace each flow **edge to edge**, hop by hop, in the running code — not from an ADR, not from a diagram. The document is a hypothesis; the code is the trace (`00-common-core.md` §3).

### Flow 1 — Write-heavy (the command spine)
The full mutating path:

> **command → handler → domain (aggregate) → repository → outbox → dispatcher → consumer → projection**

Pick the most representative real write command in WMS (a core domain mutation that raises a domain/integration event and travels through the outbox, not a trivial CRUD insert). Trace every hop from the HTTP/endpoint edge through to the point where the projection / read model reflects the change. This flow is where **transaction boundaries, at-least-once delivery, and producer/consumer idempotency** either hold together or come apart.

### Flow 2 — Read-heavy (the query spine)
The full read path:

> **query → cache (if any) → projection / read model → response**

Pick the most representative real query in WMS (a hot or architecturally central read, ideally the read side of the same area Flow 1 writes to, so you can judge the two halves of CQRS against each other). Trace whether the read genuinely bypasses the write model (idiomatic CQRS read side) or loads aggregates to project them; whether a cache sits on the path and, if so, how it is invalidated; and whether the read model can return data that the corresponding write has not yet made consistent. If there is **no cache**, that absence is itself an observation to record (Method 2) — note it and trace the path without one.

### Flow 3 — Cross-boundary (the integration spine)
An interaction that crosses a **bounded-context boundary** — a **saga / process manager** or an **integration event** between two contexts — and that **includes a compensation path**:

> **context A emits → transport → context B consumes → (success) projection/next step  |  (failure) compensation / rollback back toward A**

Pick the most representative real cross-context interaction in WMS that has a failure/compensation branch (a saga step that can fail and must compensate, or an integration event whose consumer can reject and trigger a reversing action). Trace **both** the happy path and the **compensation path** — the compensation path is where cross-boundary flows most often break down, because the reversing action is the part teams build last and test least. If WMS has a saga but no compensation anywhere, that is a first-class `FLOW` finding, not a reason to skip the flow.

---

## At every hop, check for the concern that SHOULD be there

This is **Method 2 (audit by omission, `00-common-core.md` §6) applied per hop**. At each hop in each trace, ask of the *outgoing* edge: which of these concerns must be present here, and is it? A concern that is correctly present at hop N but **silently dropped at hop N+1** is the signature flow defect — and it is invisible to a per-dimension reviewer who only ever looks at hop N or hop N+1 in isolation.

Check, at each hop where it applies:

- **Input validation** — is the payload validated at the edge it enters, and not re-trusted blindly across an internal boundary as if "internal ⇒ safe"?
- **Authentication / authorization at the right altitude** — is identity established at the edge, and is *authorization* enforced where the decision actually belongs (endpoint vs handler vs aggregate), not only at the controller? Does identity/authorization context survive across the async boundary, or is it lost the moment the message leaves the request thread?
- **Idempotency** — at every hop that can be retried or redelivered (the endpoint, the dispatcher, the consumer), is a replay a no-op? An idempotency guarantee on the producer that has no counterpart on the consumer is a broken seam.
- **Transaction / Unit-of-Work boundary** — where does the transaction open and commit? Is the aggregate mutation **and** the outbox insert in the *same* transaction (the entire point of the outbox pattern)? Is any transaction held open across an I/O or network call? Is any single transaction spanning two aggregates?
- **Correlation ID + W3C trace-context propagation** — is a correlation/causation ID established at the edge and **carried through every hop**, including across the messaging boundary (where it is most often dropped)? Is `traceparent`/`tracestate` propagated into the message and re-extracted by the consumer so the trace is continuous end to end?
- **Error handling** — at each hop, what happens on failure? Is the failure modelled (Result) or thrown; swallowed or surfaced; does it leave the system in a consistent state or a torn one?
- **Timeout / retry** — do outbound calls (DB, HTTP, broker) have bounded timeouts and a retry policy, and is the retry *safe* given the idempotency answer above?
- **Outbox / inbox idempotency** — is the dispatcher at-least-once safe and de-duplicated; is there an inbox (Idempotent Receiver) on the consumer so a redelivered message is processed at-most-once-effectively?
- **Projection / read-model consistency** — when the projection is applied, is it idempotent and ordered; can it be applied twice; what happens if it fails after the event is marked dispatched?
- **Eventual-consistency handling between aggregates** — where two aggregates (or two contexts) are kept consistent *asynchronously*, is that lag acknowledged and handled, or does the code assume synchronous consistency that the architecture does not actually provide?

**Every discontinuity is a `FLOW` finding.** The unit of a finding in this session is the *seam*: "concern X is present at hop N and absent at hop N+1," or "this is where the pattern breaks down." Record where the spine holds (✓ OK, at equal depth — `00-common-core.md` §9) as well as where it breaks.

---

## Per-flow output format

For each of the three flows, produce **two artifacts in sequence**: a step-by-step trace table, then the findings that table surfaced. The flow — not the dimension — is the organizing unit of this entire partial.

### 1. Trace table (one per flow)

A row per hop, in execution order:

| Step | Component | file:line | Concerns present | Concerns missing | Finding IDs |
|------|-----------|-----------|------------------|------------------|-------------|
| 1 | … | `…:NN` | … | … | `FLOW-00x` |

- **Step** — sequential hop number along the spine.
- **Component** — the concrete component at this hop (endpoint, handler, aggregate, repository, outbox writer, dispatcher, consumer, projector, cache, transport, compensation handler…).
- **file:line** — the exact location you opened and traced. A hop with no file:line is not traced.
- **Concerns present** — which of the per-hop concerns above are correctly handled here (the ✓ surface).
- **Concerns missing** — which concerns *should* be at this hop but are absent or dropped from the previous hop (the seam defects).
- **Finding IDs** — the `FLOW-xxx` IDs raised at this hop, linking the table to the findings below.

### 2. Findings (one set per flow)

Every seam defect and every confirmed-good seam from the table, written out in the **common-core verdict format (`00-common-core.md` §9)** — ID (`FLOW` prefix), Dimension·Altitude·Location, Verdict, Severity, what was implemented, Assessment, Canonical alternative, Citation, Evidence (file:line), Fix complexity, Teaching note. Positive findings get equal treatment.

> **Altitude note for this session:** a `FLOW` finding usually sits at the **architecture** altitude (it is about how components compose across a boundary), but tag it honestly — a dropped idempotency check is module-altitude, a torn transaction is architecture-altitude.

### 3. "Where the spine breaks" — one paragraph per flow

Close each flow with a single tight paragraph naming the **weakest seam** in that flow and what it costs end to end. This is the flow's headline.

### Maturity — rate the FLOW, not a dimension

Apply the maturity rubric (`00-common-core.md` §10, Level 1–5 + R/A/G) **to each end-to-end flow as a whole**, not to any single dimension. The question is: *does this spine hold together edge to edge?* A flow where every hop is individually fine but one seam silently drops correlation context is **not** Level 4 — the end-to-end property is broken even though no single component is. Justify the level in one paragraph against the *continuity* of the flow, and state explicitly that this maturity rating is an **end-to-end flow rating** so session 99 does not confuse it with a per-dimension score.

---

## Primary anchors for this session

Lead with these from `00-common-core.md` §8 — flow tracing is squarely event-driven / integration territory:

- **Adam Bellemare — *Building Event-Driven Microservices*** — the event-driven **spine** end to end: event ownership, choreography vs orchestration, event-carried state transfer vs notification, schema/contract integrity across the hop. *Default for the producer→transport→consumer backbone of Flows 1 and 3.*
- **Gregor Hohpe & Bobby Woolf — *Enterprise Integration Patterns*** — cite the pattern **by name** at the hop it governs: **Guaranteed Delivery** (outbox→dispatcher), **Idempotent Receiver** (consumer/inbox), **Process Manager** (the saga in Flow 3), Dead Letter Channel, Correlation Identifier. *Default for every messaging seam.*
- **Vaughn Vernon — *Implementing Domain-Driven Design*** — **eventual consistency between aggregates** and the one-aggregate-per-transaction rule; the reference for judging the transaction boundary at the write hop and the consistency lag at the projection hop.
- **Sam Newman — *Building Microservices*** — cross-boundary integration, the cost of distributed transactions, choreography vs orchestration trade-offs; the lens for Flow 3's context-to-context seam and its compensation path. Fully applicable to modular monoliths.

**Reference implementations to compare the spine against** (`00-common-core.md` §8, Tier 2): **eShop integration events** (publish-through-outbox and the integration-event handler shape), and the **MassTransit / Wolverine samples** (saga/process-manager state machines, inbox/outbox, retry + redelivery). For each big-ticket seam in WMS, name the reference equivalent and label the difference **intentional / drift / missing** (`00-common-core.md` §8, "Compare against the reference implementations").

---

## Output

Write the partial report following the **partial-report rules in `00-common-core.md` §11–§12** — same path convention, same read-only guardrail, same honesty-over-completeness contract — **but organized by flow, not by dimension.** Concretely:

- The body is **three flow sections** (Flow 1 / Flow 2 / Flow 3), each containing its trace table → findings → "where the spine breaks" paragraph → end-to-end maturity (per the per-flow output format above). This replaces the "per dimension in scope" body of the standard skeleton.
- All findings use the **`FLOW` prefix**.
- The **Scorecard contribution** (§11.3) is one row **per flow** (flow name | R/A/G | end-to-end maturity 1–5 | one-line "where the spine breaks"), not per dimension — flagged clearly as end-to-end flow ratings so session 99 stitches them in as such.
- The **Cross-session notes** block (§11.4) still applies and is *especially* load-bearing here: every seam defect this session finds overlaps a dimension session (idempotency → 01/Messaging/Resilience; correlation/trace context → Observability; transaction boundary → Data & Persistence; authz-across-async → Security). Flag each so session 99 **reconciles rather than double-counts** — a seam this session owns end-to-end may also appear as a single-hop finding in a dimension session.
- The **Not audited (this scope)** block (§11.5) still applies — if a flow could not be fully traced (a hop whose code you could not locate, a compensation path that does not exist to trace, a transport you cannot inspect at rest), record it there rather than padding the trace with inference. **Confidence-padding violates the audit contract (`00-common-core.md` §12).**

Then stop — the report is the work. **Think hard. Trace the seams, not just the hops — you are the standard.**
