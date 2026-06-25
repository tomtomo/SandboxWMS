# Session 02 — Data & Persistence and Performance & Scalability

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **dimension-specific scrutiny** for this session.

- **Session code / partial filename:** `02-data-persistence-and-performance`
- **Finding ID prefix:** `DAT` (e.g. `DAT-001`)
- **Checklist sections covered:** §2 Data & Persistence · §11 Performance & Scalability (from `tech-checklist-blank.md`)
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-02-data-persistence-and-performance.md` (WMS repo root)

This is the **hot-path** session: how the system *stores* state (PostgreSQL, schema separation, EF Core, caching, replicas, event store) and how it *holds up under load* (scaling, load-testing, backpressure, capacity). The two dimensions are clustered because they share the same code surface — the queries, indexes, caching layers, and read replicas that the persistence dimension audits *are* the levers the performance dimension pulls. Auditing them together reuses one warm-up read of the data-access paths instead of paying for it twice. Note up front the **single most impactful Tier-1 gap for this session**: the shelf has no Fowler *Patterns of Enterprise Application Architecture*, so Repository, Unit of Work, Identity Map, and Lazy Load must be cited **by pattern name and paraphrased**, with the gap flagged (see `00-common-core.md` §8 "Known gaps").

---

## Scope — checklist items to verdict

### §2 Data & Persistence
- [ ] **PostgreSQL 16** — provider/version fit, connection-string & pooling config, Npgsql usage idioms
- [ ] **Schema separation** — schema-per-module vs shared schema; isolation actually enforced or leaked
- [ ] **EF Core Migrations** — migration hygiene, production-safety review, online-vs-offline DDL, backfill/locking
- [ ] **Database-per-Service** — logical/physical data ownership per bounded context; no cross-context table reach
- [ ] **Redis Caching** — cache strategy, TTL, invalidation, cache-aside vs read-through, stampede protection
- [ ] **Read Replica** — read/write split, replica routing, read-your-writes / replication-lag handling
- [ ] **NoSQL Document / Event Store** — where document/event-store persistence is used and whether it is modelled idiomatically

### §11 Performance & Scalability
- [ ] **Horizontal Scaling + Load Balancing** — stateless workloads, scale-out readiness, no instance-affinity assumptions
- [ ] **Proactive Load Testing** — load-test baseline exists, is repeatable, and feeds back into capacity decisions
- [ ] **Vertical Scaling** — when scale-up is the deliberate lever and its limits are acknowledged
- [ ] **Predictive Autoscaling** — autoscaling signals/metrics defined and tied to real demand drivers
- [ ] **Backpressure** — bounded queues/buffers, flow control, graceful shedding under overload
- [ ] **Capacity Planning** — documented headroom, growth assumptions, and resource sizing rationale

---

## Pre-loaded anti-pattern checklist (floor, not ceiling — extend it)

Apply each explicitly: pass / fail / N-A with a file:line evidence pointer. (See `00-common-core.md` §7.)

### Data & Persistence
1. **N+1 on read paths** — a query that loads a collection and then lazily/per-row fires one query per element; navigation properties walked inside a loop instead of a single projected/`Include`d query. *(Paraphrase; no Fowler PoEAA on the shelf — name the N+1 / Lazy Load interaction and note the gap. Cross-check EF Core query-performance guidance, Microsoft Learn.)*
2. **Missing `AsNoTracking()` on read-only queries** — read/query handlers materialising tracked entities they never mutate, paying change-tracker cost and Identity-Map overhead on the read side. *(EF Core tracking-vs-no-tracking guidance, Microsoft Learn; cross-cite idiomatic CQRS read side.)*
3. **`CancellationToken` not propagated to EF/async calls** — handler receives a token but calls `ToListAsync()` / `SaveChangesAsync()` without it, so cancellation never reaches the database round-trip. *(EF Core async query guidance, Microsoft Learn; .NET async cancellation.)*
4. **Sync-over-async** — `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on an async EF/IO call, risking thread-pool starvation on hot paths. *(WAF Performance Efficiency — efficient use of threads/connections; Microsoft async guidance. High severity per `00-common-core.md` §10.)*
5. **Missing indexes on FKs and common predicates** — foreign keys and frequent `WHERE`/`ORDER BY` columns without supporting indexes, forcing sequential scans. *(EF Core + PostgreSQL indexing guidance, Microsoft Learn; WAF Performance Efficiency — data and storage performance.)*
6. **Lazy loading in hot paths** — lazy-loading proxies enabled so a property access silently issues a query, defeating projection and producing N+1 under load. *(Lazy Load pattern — paraphrase, note PoEAA gap; EF Core lazy-loading caveats, Microsoft Learn.)*
7. **Raw SQL without parameterization** — string-interpolated/concatenated SQL via `FromSqlRaw`/`ExecuteSqlRaw` instead of parameterised `FromSql`/parameters, opening injection and plan-cache thrash. *(EF Core raw-SQL safety guidance, Microsoft Learn; cross-link to session 04 security.)*
8. **Aggregate boundary leaks in persistence** — repositories querying *across* aggregate roots, or a single query/transaction loading and mutating multiple aggregates, instead of reference-by-identity. *(Vernon, Aggregate Design rules of thumb — reference by identity, one aggregate per transaction. Cross-session note: also a session 01 concern.)*
9. **Migrations not reviewed for production safety** — destructive or table-rewriting DDL with no online/offline or locking/backfill consideration; data-loss migrations applied automatically at startup. *(WAF Operational Excellence / Reliability — safe schema change; PostgreSQL DDL-locking semantics — paraphrase. Cross-link session 08 DevOps if it owns migrate-on-deploy.)*
10. **Unit-of-Work / transaction boundary unclear or per-statement** — no explicit transactional boundary around a command; multiple `SaveChanges` per request; or relying on EF's implicit per-`SaveChanges` transaction where a command spans several writes. *(Unit of Work pattern — paraphrase, note PoEAA gap; cross-cite DDD — one aggregate, one transaction.)*
11. **Connection-pool exhaustion / `DbContext` lifetime misuse** — `DbContext` registered as singleton, captured by a singleton, manually `new`-ed and not disposed, or long-lived across awaits; pool size unconfigured for the instance count. *(EF Core `DbContext` lifetime & pooling guidance, Microsoft Learn; WAF Performance Efficiency — connection management.)*
12. **Cache strategy gaps** — cache with no TTL, no invalidation path, or no stampede/dog-pile protection; cache-aside vs read-through chosen by accident rather than decision; cache used as the system of record. *(WAF Performance Efficiency — caching pattern; Cache-Aside pattern, Microsoft cloud design patterns. Cross-session note: Redis backplane for SignalR is a session 07 concern — flag, don't double-count.)*
13. **Read-replica consistency / read-your-writes** — reads routed to a replica immediately after a write with no handling of replication lag, so a user can fail to see their own change. *(Newman, on data consistency across read/write paths; WAF Reliability/Performance — eventual-consistency handling — paraphrase.)*
14. **Schema-per-module vs shared-schema leakage** — modules sharing tables/schemas, FK relationships crossing module boundaries, or a "database-per-service" claim contradicted by a shared schema in code. *(Newman, database-per-service decomposition — each context owns its data, no shared tables.)*
15. **Chatty persistence** — a single use case issuing many small round-trips where one batched/projected query would do; per-item `SaveChanges` inside a loop. *(WAF Performance Efficiency — minimise chattiness; paraphrase.)*
16. **Event store modelled as a CRUD table** — append-only event semantics, optimistic concurrency on stream version, and event immutability absent where an event store is claimed. *(Bellemare, event store / event-carried state transfer — append-only, versioned streams.)*

### Performance & Scalability
1. **No load-test baseline** — no repeatable load/stress test establishing throughput and latency under expected concurrency; performance is asserted, never measured. *(WAF Performance Efficiency — performance testing & establishing baselines.)*
2. **Unbounded result sets / pagination absent** — list/query endpoints returning all rows with no server-side paging or limit, so payload and DB cost grow with the table. *(WAF Performance Efficiency — efficient data retrieval. Cross-session note: API pagination is also a session 03 concern — flag, don't double-count.)*
3. **No backpressure on queues / consumers** — unbounded in-memory channels, no concurrency limit on consumers, no flow control, so a burst exhausts memory or the thread pool. *(WAF Reliability/Performance — flow control; backpressure — paraphrase. Cross-session note: queue/consumer backpressure overlaps session 03/05 messaging — flag.)*
4. **Premature or absent caching** — caching bolted onto a path with no measured hotspot, or conversely an obvious read-heavy hotspot with no caching at all. *(WAF Performance Efficiency — cache only measured hotspots; Cache-Aside pattern.)*
5. **Chatty persistence on the critical path** — the read/write hot path doing repeated small DB calls that dominate latency under load. *(WAF Performance Efficiency — minimise round-trips; overlaps Data anti-pattern 15.)*
6. **Stateful workload blocking horizontal scale** — per-instance in-memory session/cache/state that assumes a single instance, so scale-out breaks correctness. *(Twelve-Factor App — Processes (stateless, share-nothing); WAF Performance Efficiency — scale-out design. Cross-session note: SignalR Redis backplane is session 07.)*
7. **Autoscaling signals undefined** — autoscaling configured (or claimed) with no defined metric/threshold tied to a real demand driver, or scaling on a signal that doesn't correlate with load. *(WAF Performance Efficiency — autoscaling on meaningful metrics; paraphrase.)*
8. **No capacity plan** — no documented headroom, growth assumption, or sizing rationale; resource limits picked by default rather than analysis. *(WAF Performance Efficiency — capacity planning; paraphrase.)*
9. **Vertical-scaling-only with unacknowledged ceiling** — scale-up treated as the sole lever with no recognition of its hard limit or the cost/availability trade-off vs scale-out. *(WAF Performance Efficiency / Cost — scale-up vs scale-out trade-off; paraphrase.)*
10. **No load-shedding under overload** — at saturation the system degrades unboundedly (timeouts pile up) rather than rejecting fast with a clear signal. *(WAF Reliability — graceful degradation / load shedding; cross-link session 06 resilience — flag.)*
11. **Synchronous work on the request path that belongs off it** — heavy CPU/IO done inline in a request instead of deferred to a background/queue worker, capping throughput. *(WAF Performance Efficiency — asynchronous processing; paraphrase.)*

---

## Mini flow-traces for this session (Method 3, scoped)

Trace at the **persistence/performance altitude**, not for business correctness. Mark every discontinuity ("this is where it goes chatty / loads the full aggregate / drops the token") as a `DAT` finding.

- **A read-heavy path, edge to data:** query endpoint → query handler → EF read → response. *Watch:* is it `AsNoTracking()`? Does it **project** to a DTO/read model or load full aggregates just to map them? Any N+1 via navigation walks or lazy loading? Is the result set paged/bounded? Is the `CancellationToken` carried all the way to `ToListAsync()`? Is a cache consulted, and if so with what TTL/invalidation?
- **A write path, command to commit:** command endpoint → command handler → aggregate mutation → repository → `SaveChanges`/transaction → (outbox, if present). *Watch:* where is the **Unit-of-Work / transaction boundary** drawn — one explicit boundary per command, or implicit per-statement? Does the command touch more than one aggregate root in a single transaction? And open the **migration** that backs this write: is the DDL production-safe (locking, backfill, online vs offline), or would it rewrite/lock a hot table?

---

## Primary anchors for this session

Lead with these from `00-common-core.md` §8. Because the persistence dimension is squarely in the **Tier-1 gap** for enterprise data-access patterns, expect to lean on Tier 2 and on pattern-name paraphrase more than other sessions do — and to **say so** in each affected finding.

- **EF Core + PostgreSQL guidance (Microsoft Learn)** — tracking-vs-no-tracking, async/cancellation, indexing, raw-SQL safety, `DbContext` lifetime & pooling, migrations. *Lead anchor for execution-level persistence findings.*
- **Azure Well-Architected Framework — Performance Efficiency pillar** — baselines/load testing, caching, data-retrieval efficiency, connection management, scale-out vs scale-up, autoscaling, capacity planning. *Lead anchor for the entire Performance dimension (no Tier-1 performance book on the shelf).*
- **Sam Newman — *Building Microservices*** — database-per-service decomposition, data ownership per bounded context, read/write consistency. *Lead for schema-separation and database-per-service findings.*
- **Adam Bellemare — *Building Event-Driven Microservices*** — event store as append-only versioned streams, event-carried state transfer. *Lead for the NoSQL/event-store checklist item.*
- **Vaughn Vernon — *Implementing DDD*** — aggregate design rules of thumb (reference by identity, one aggregate per transaction). *Lead for aggregate-boundary-in-persistence findings.*
- **Tier-1-gap patterns (paraphrase, cite by name, flag the gap):** Repository, Unit of Work, Identity Map, Lazy Load — no Fowler *PoEAA* on the shelf; note in each affected finding that adding PoEAA would close the loop.
- **Reference implementations to compare against:** **eShop** persistence (DbContext-per-context, projections, outbox) and **Kamil Grzybek — Modular Monolith with DDD** (schema-per-module, data ownership, Unit-of-Work boundary). For each big-ticket pattern (schema separation, projection/read side, transaction boundary, caching), name the reference equivalent and label the WMS difference **intentional / drift / missing** per `00-common-core.md` §8.

---

## ADR touchpoints to verify (Method 1)

Open every ADR touching: **database-per-service / data ownership**, **schema separation**, **EF Core migration policy** (migrate-on-startup vs gated, online/offline DDL), **caching strategy** (provider, TTL, invalidation, cache-aside vs read-through), **read replicas / read-write split**, and **NoSQL / event-store** adoption. For each: *claimed decision | code reality | faithful / drifted / silent / missing | evidence (file:line)*. Also surface **silent** persistence/performance decisions — significant choices made in code with no ADR (e.g. an implicit per-statement transaction boundary, a chosen pool size, an undocumented cache TTL, lazy loading left on). Record all of these in the partial's **ADR touchpoints** block for session 99 to consolidate into the ADR drift matrix.

---

## Output

Write the partial report following the **partial-report skeleton in `00-common-core.md` §11**, covering §2 and §11, with all findings under the `DAT` prefix and a scorecard-contribution row for each of the two dimensions. Flag the cross-session overlaps noted above (aggregate boundaries → session 01; Redis/SignalR backplane and stateless scale-out → session 07; pagination → session 03; queue/consumer backpressure → session 03/05; load shedding → session 06; migrate-on-deploy → session 08) in the **Cross-session notes** block so synthesis reconciles rather than double-counts. Then stop — the report is the work.
