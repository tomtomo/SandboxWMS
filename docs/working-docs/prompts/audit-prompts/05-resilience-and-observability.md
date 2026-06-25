# Session 05 — Resilience & Observability

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **dimension-specific scrutiny** for this session.

- **Session code / partial filename:** `05-resilience-and-observability`
- **Finding ID prefix:** `RES` (e.g. `RES-001`)
- **Checklist sections covered:** §8 Resilience · §9 Observability (from `tech-checklist-blank.md`)
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-05-resilience-and-observability.md` (WMS repo root)

This is the **runtime-robustness** session: whether the system *survives* the failures it will inevitably meet (timeouts, transient faults, tripped circuits, poison messages) and whether you can *see* it doing so (structured logs, traces, metrics, alerts). The two dimensions are clustered because they are inseparable in practice — you cannot trust resilience you cannot observe, and a retry/circuit-breaker that fires silently is worse than none. Both attach to the *same surfaces*: the outbound dependency calls (HTTP, DB) and the messaging boundaries (Outbox dispatch, consumers, DLQ). This is also the session where **Method 2 (audit by omission)** earns its keep, because most resilience and observability defects have no file to "open and check" — there is no `MissingCircuitBreaker.cs`. Record ADR touchpoints carefully — session 99 builds the ADR drift matrix from them.

---

## Scope — checklist items to verdict

### §8 Resilience
- [ ] **Polly — HTTP Client Timeout** — per-attempt and total request timeouts on outbound `HttpClient` calls
- [ ] **Polly — Retry with Exponential Backoff** — bounded retries, exponential backoff with jitter, only on transient/idempotent operations
- [ ] **Circuit Breaker** — outbound HTTP/DB calls trip open on sustained failure, half-open probe, fail fast while open
- [ ] **Bulkhead Isolation** — concurrency/connection partitioning so one saturated dependency can't exhaust shared pools
- [ ] **Request Hedging** — parallel/duplicate requests to cut tail latency, used only where safe (idempotent reads)

### §9 Observability
- [ ] **ILogger (Microsoft.Extensions.Logging)** — logging abstraction used at source, not a concrete sink
- [ ] **Serilog** — structured logging with consistent enrichers, sinks, and message templates
- [ ] **OpenTelemetry** — traces/metrics (and logs) exported via OTel SDK with proper instrumentation and context
- [ ] **Correlation ID** — a request/causation identifier present and propagated across process and async boundaries
- [ ] **Centralized Log Aggregation** — logs shipped to a queryable store, not just console/file
- [ ] **Operational Dashboard** — golden-signal dashboards exist and are version-controlled
- [ ] **SLO-Driven Alerting** — alerts tied to SLOs/SLIs and error budgets, not raw threshold noise
- [ ] **Real User Monitoring (RUM)** — client-side experience telemetry (latency, errors) captured from real sessions

---

## Pre-loaded anti-pattern checklist (floor, not ceiling — extend it)

Apply each explicitly: pass / fail / N-A with a file:line evidence pointer. (See `00-common-core.md` §7.) Note the **Tier-1 resilience gap up front**: there is no Nygard *Release It!* on the shelf, so the stability patterns below are cited **by name** (Circuit Breaker, Bulkhead, Timeout, Fail Fast, Steady State) and **paraphrased**, with *execution* anchored to the Polly docs and the WAF Reliability pillar. Observability is squarely **Tier-2** territory — anchor to the OpenTelemetry specification and Microsoft Learn, and paraphrase where neither states the principle in the form you're claiming.

### Resilience
1. **Polly policies declared but not wired** — a resilience pipeline/policy is defined but never attached to the `HttpClient` via `AddResilienceHandler` / `AddPolicyHandler` (or the typed client doesn't flow through the handler), so the outbound call runs naked. *(Stability pattern — Timeout/Circuit Breaker, paraphrase; Polly docs — resilience pipeline registration on the `HttpClient` handler chain.)*
2. **Circuit breaker missing on outbound calls** — outbound HTTP to other services/providers and DB calls have retry/timeout but no breaker, so a hard-down dependency is hammered indefinitely and the failure is not contained. *(Stability pattern — Circuit Breaker + Fail Fast, paraphrase; WAF Reliability — handle transient faults / circuit breaker guidance.)*
3. **Retry without idempotency guarantees** — retries (or hedging) sit on top of a *non-idempotent* operation (a mutating POST, a non-idempotent consumer), so a retried call can double-apply an effect. *(Hohpe & Woolf — Idempotent Receiver; the retry is only safe if the receiver is idempotent.)* **Cross-session note:** idempotency keys / Idempotent Consumer are owned by session 03 — assess only whether the *retry surface* assumes idempotency it doesn't have, and defer the consumer-side mechanism to 03.
4. **Outbox dispatcher not at-least-once safe** — the Outbox poller marks a message dispatched before the broker has durably accepted it, or a crash between publish and mark loses or silently drops the message, breaking Guaranteed Delivery. *(Hohpe & Woolf — Guaranteed Delivery; Bellemare — outbox/at-least-once delivery.)* **Cross-session note:** Outbox correctness is co-owned with session 03 — here, judge it strictly as a *resilience* property (at-least-once, retry-safe dispatch).
5. **DLQ exists but has no inspection/replay path** — a Dead Letter Channel/queue is configured, but there is no operator workflow to inspect, fix, and re-drive poisoned messages, so they rot. *(Hohpe & Woolf — Dead Letter Channel; the channel is incomplete without a documented inspection/redelivery path.)* **Cross-session note:** DLQ topology is co-owned with session 03 — assess the *replay/operability* angle here.
6. **Timeouts unset or set too high** — outbound `HttpClient` relies on the 100-second framework default (or an effectively infinite DB command timeout), so a slow dependency turns into thread/connection exhaustion instead of a fast, bounded failure. *(Stability pattern — Timeout, paraphrase; WAF Reliability — set realistic timeouts.)*
7. **Bulkhead absent on shared pools** — no concurrency limiter / partitioning around a shared `HttpClient`, DB connection pool, or thread pool, so one slow dependency consumes all capacity and cascades the outage system-wide. *(Stability pattern — Bulkhead, paraphrase; Polly docs — rate-limiter / bulkhead isolation strategy.)*
8. **Request hedging misused** — hedging fires duplicate requests against *non-idempotent* or expensive operations, amplifying load and risking double effects, instead of being confined to safe idempotent reads. *(Stability pattern — paraphrase; WAF Performance/Reliability — hedging only for idempotent operations.)*
9. **No fallback / fail-fast strategy** — when a dependency is down there is neither a graceful fallback (cached/default response) nor a deliberate fail-fast; the call just hangs or throws an unhandled exception to the caller. *(Stability pattern — Fail Fast + graceful degradation, paraphrase; WAF Reliability — degrade gracefully.)*
10. **Health checks not wired to orchestrator readiness** — `/health` endpoints exist but the container/orchestrator readiness probe isn't pointed at them (or points at a liveness-style check), so traffic is routed to instances that aren't actually ready. *(WAF Reliability — health endpoint monitoring / Health Endpoint Monitoring pattern.)* **Cross-session note:** health-check wiring to the orchestrator overlaps sessions 06 (infra) and 07 (DevOps) — flag, don't double-score.
11. **Readiness vs liveness not separated** — a single health check conflates "the process is alive" with "the process can serve traffic" (e.g. a DB ping in the liveness probe), so a transient dependency blip kills and restarts an otherwise-healthy pod. *(WAF Reliability — distinguish liveness from readiness; Kubernetes probe semantics, paraphrase.)*
12. **Retry storms / no jitter** — exponential backoff without randomized jitter, or unbounded retry counts, so many clients retry in lockstep and synchronize into a thundering herd against a recovering dependency. *(WAF Reliability — exponential backoff *with jitter*; Polly docs — `DelayBackoffType.Exponential` + `UseJitter`.)*

### Observability
1. **Structured-logging fields inconsistent across modules** — the same concept is logged under different property names (`UserId` vs `userId` vs `user`), or some modules use message templates and others string-interpolate, so logs can't be queried uniformly. *(OpenTelemetry — semantic conventions for attributes; Serilog — message templates over string interpolation, paraphrase.)*
2. **Correlation ID not propagated across the async/messaging boundary** — the correlation/trace id rides the inbound HTTP request but is *not* carried onto the Outbox message / broker envelope, so a request can't be followed once it crosses into messaging. *(OpenTelemetry — context propagation across process boundaries; W3C Trace Context.)* **Cross-session note:** correlation-ID propagation across messaging overlaps session 03 — reconcile, don't double-count.
3. **Trace context dropped at the messaging boundary** — W3C `traceparent`/`tracestate` is not injected into outgoing messages or not extracted by the consumer, so the distributed trace breaks into two disconnected halves at the queue. *(OpenTelemetry — trace context propagation / messaging instrumentation; W3C Trace Context.)*
4. **Log levels misused** — expected/handled conditions logged at `Error`, transient retries logged at `Warning` when they should be `Information`/`Debug`, or genuine faults swallowed at `Information`, so alert routing on level is meaningless. *(Microsoft Learn — `LogLevel` semantics; paraphrase the error-vs-warning-vs-info contract.)*
5. **Metrics exist but no SLO/SLI tied to them** — counters/histograms are emitted but nothing defines the target (e.g. p99 latency objective, error-rate budget) the metric is meant to defend, so the metric is decorative. *(WAF Reliability/Operational Excellence — define SLIs/SLOs; OTel metrics, paraphrase.)* **Cross-session note:** SLO/SLI definition is co-owned with sessions 07 and 09 — here assess only whether the *telemetry actually supports* an SLI; defer SLO governance/error-budget policy to those sessions.
6. **Sensitive data (PII) in traces/logs** — credentials, tokens, or warehouse/user PII land in log messages, span attributes, or exception payloads with no redaction/destructuring policy. *(OWASP — sensitive-data exposure; OTel — attribute hygiene, paraphrase.)* **Cross-session note:** PII-in-telemetry / log scrubbing overlaps session 04 (security & compliance) — flag, don't double-score the scrubbing mechanism.
7. **`ILogger` bypassed or wrapped wrong** — code logs to `Console.WriteLine`, a static logger, or `new`s a concrete Serilog logger instead of taking `ILogger<T>` by DI, breaking the abstraction and the per-category level config. *(Microsoft Learn — `ILogger<T>` via DI; paraphrase the logging-abstraction-at-the-source principle.)*
8. **Health/telemetry not exported anywhere** — OpenTelemetry is configured with only a console exporter (or none), or Serilog writes only to a local file, so in a multi-instance deployment there is no centralized, queryable signal. *(OTel — exporters/OTLP pipeline; WAF Operational Excellence — centralized aggregation, paraphrase.)*
9. **Dashboards and alerts absent or not version-controlled** — golden-signal dashboards (latency, traffic, errors, saturation) and alert rules either don't exist or live only in a portal UI, not as code in the repo, so they can't be reviewed, diffed, or reproduced. *(WAF Operational Excellence — monitoring as code / golden signals; paraphrase.)*
10. **No trace–log correlation** — logs don't carry the active `TraceId`/`SpanId`, so an operator who finds a slow trace can't pivot to its logs (and vice versa), defeating the point of having both. *(OpenTelemetry — log/trace correlation via trace context; paraphrase.)*
11. **Cardinality/over-instrumentation hazards** — high-cardinality values (user ids, order ids, raw URLs) used as metric dimensions or span names, which will blow up the backend, *or* the opposite: no instrumentation at all on the hot paths that matter. *(OTel — metric cardinality guidance; paraphrase.)*
12. **Activity/span not created at boundaries** — outbound HTTP, DB, and message-handling code does no manual `ActivitySource` instrumentation where auto-instrumentation doesn't reach, leaving blind spots in the trace exactly at the dependency edges this session cares about. *(OpenTelemetry — `ActivitySource`/manual instrumentation; paraphrase.)*

---

## Mini flow-traces for this session (Method 3, scoped)

Trace at the **runtime-robustness altitude**, not for business correctness. Both traces deliberately follow the *same surfaces* — the outbound call edge and the messaging boundary — because that is where resilience and observability either both hold or both break:

- **Outbound dependency call — the resilience chain:** an application/infrastructure adapter making an outbound call (HTTP to a provider or a DB command) → **timeout** (per-attempt + total) → **retry with exponential backoff + jitter** → **circuit breaker** (trips open on sustained failure) → **fallback or fail-fast**. *Watch:* is the Polly pipeline actually attached to *this* client's handler chain, or just declared in DI? Is the order sane (timeout inside retry, breaker wrapping)? On a tripped breaker, does the caller get a fast, observable failure — or a hang? Is each stage emitting a log/metric/span, or is the resilience invisible? Mark every stage that is missing or silent as a `RES` finding.
- **Request crossing an async messaging boundary — the context chain:** an inbound request that produces a domain/integration event → Outbox row → dispatcher publish → broker → consumer. *Watch:* does the **correlation id** and the **W3C trace context** (`traceparent`) get written onto the Outbox message / broker envelope and *re-extracted* by the consumer, so the trace and logs span the whole hop? Or does the trace die at the queue and the consumer start a fresh, unlinked context? Is the dispatch at-least-once and retry-safe? Mark the exact point the context (or the delivery guarantee) breaks.

Mark every discontinuity ("this is where resilience/observability breaks down") as a `RES` finding.

---

## Primary anchors for this session

Lead with these from `00-common-core.md` §8:

- **Gregor Hohpe & Bobby Woolf — *Enterprise Integration Patterns*** — the messaging-resilience backbone: cite **Idempotent Receiver**, **Dead Letter Channel**, and **Guaranteed Delivery** by name for the Outbox/DLQ/retry surface.
- **Adam Bellemare — *Building Event-Driven Microservices*** — at-least-once delivery, outbox semantics, and event-handling robustness across the messaging boundary.
- **Polly documentation** *(Tier 2)* — the canonical anchor for resilience **execution** in .NET: resilience pipelines, strategy ordering, `AddResilienceHandler` wiring, backoff-with-jitter, breaker and rate-limiter/bulkhead strategies.
- **Azure Well-Architected Framework — Reliability pillar** *(Tier 2)* — the **posture** anchor: transient-fault handling, circuit breaker, realistic timeouts, graceful degradation, health endpoint monitoring.
- **OpenTelemetry specification** *(Tier 2)* — the **observability spec** anchor: context propagation, W3C Trace Context, semantic conventions, traces/metrics/logs model, exporters.
- **Microsoft Learn** *(Tier 2)* — `ILogger`/`LogLevel` semantics, Serilog and OpenTelemetry wiring in ASP.NET Core, health checks, `ActivitySource` instrumentation (verify current via tool when in doubt).

**Tier-1 gap to state explicitly:** there is **no Nygard *Release It!*** on the shelf. Cite the stability patterns (Circuit Breaker, Bulkhead, Timeout, Fail Fast, Steady State) by name and **paraphrase** the principle, anchoring *execution* to Polly and *posture* to the WAF Reliability pillar; note that adding *Release It!* would close the loop on the resilience theory. Observability theory is likewise a known Tier-1 gap (no Sridharan *Distributed Systems Observability*) — rely on OTel docs and Microsoft Learn.

**Reference implementations to compare against:** **eShop** (Polly resilience handlers on typed clients, OpenTelemetry + health-check wiring), **MassTransit / Wolverine samples** (retry/redelivery, DLQ, consumer idempotency, trace-context propagation across the broker). For each big-ticket pattern in WMS (Polly pipeline wiring, Outbox at-least-once, DLQ replay, correlation-ID/trace-context propagation), name the reference equivalent and label the difference **intentional**, **drift**, or **missing**.

---

## ADR touchpoints to verify (Method 1)

Open every ADR touching: **resilience policy** (Polly — retry/timeout/circuit-breaker strategy and where it's applied), **health checks** (liveness/readiness split, orchestrator wiring), **logging stack** (Serilog — sinks, enrichers, structured-logging conventions), **OpenTelemetry** (tracing/metrics, exporters, context propagation), and **alerting / SLO** (alert routing, SLI/SLO definition, error budgets). For each: *claimed decision | code reality | faithful / drifted / silent / missing | evidence (file:line)*. Also surface **silent** decisions — e.g. a timeout/backoff value or a breaker threshold chosen in code with no ADR, a DLQ with no documented replay procedure, a log field convention enforced only by habit. Record all of these in the partial's **ADR touchpoints** block for session 99 to consolidate.

---

## Output

Write the partial report following the **partial-report skeleton in `00-common-core.md` §11**, covering §8 and §9, with all findings under the `RES` prefix and a scorecard-contribution row for **each** of the two dimensions (Resilience; Observability). Then stop — the report is the work.
