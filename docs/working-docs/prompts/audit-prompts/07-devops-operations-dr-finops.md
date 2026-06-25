# Session 07 — DevOps & Operations, DR & BCP, Cost / FinOps

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **dimension-specific scrutiny** for this session.

- **Session code / partial filename:** `07-devops-operations-dr-finops`
- **Finding ID prefix:** `OPS` (e.g. `OPS-001`)
- **Checklist sections covered:** §13 DevOps & Operations · §18 DR & BCP · §19 Cost Management / FinOps (from `tech-checklist-blank.md`)
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-07-devops-operations-dr-finops.md` (WMS repo root)

This is the **run-and-govern** session: not how the system is *built*, but how it is *delivered, kept available, and kept economical* over time. The three dimensions cluster because they are all operational-governance concerns that converge on the same two surfaces — the **deployment pipeline** and the **runbook**. CI/CD decides what reaches production and how safely; DR/BCP decides whether the system survives a failure and how fast it comes back; FinOps decides whether it stays affordable while it runs. None of these live in a single source file you can open — they live in pipeline definitions, IaC, scripts, runbooks, dashboards, and (often) in their *absence*. That makes **Method 2 (audit by omission)** the dominant lens here: a missing canary stage, an untested backup, or an absent budget alert is the finding. These dimensions are squarely **Tier-2 territory** — lead with the cloud framework pillars and operational-excellence canon, and paraphrase where the shelf is thin.

---

## Scope — checklist items to verdict

### §13 DevOps & Operations
- [ ] **Basic CI** — does every push at least build + run tests automatically
- [ ] **CI/CD Pipeline** — full path from commit to a deployable/deployed artifact, gated
- [ ] **Deployment Strategy** — a defined, repeatable release mechanism beyond "push"
- [ ] **Canary / Blue-Green Deployment** — risk-limited rollout where it should exist
- [ ] **Progressive Delivery** — staged exposure tied to metrics / feature flags
- [ ] **SLO / SLI Definition** — service objectives and indicators defined and measurable
- [ ] **Error Budget Governance** — error budget derived from SLOs and actually used to gate change
- [ ] **On-Call Rotation** — defined ownership and escalation for production incidents
- [ ] **Blameless Postmortem** — incident-review discipline that produces durable learning

### §18 DR & BCP
- [ ] **Automated Database Backup** — backups taken automatically on a schedule
- [ ] **Defined RTO / RPO** — recovery-time and recovery-point objectives stated and justified
- [ ] **DR Runbook** — version-controlled, executable recovery procedure
- [ ] **Monthly Restore Testing** — backups actually restored on a cadence (an untested backup is not a backup)
- [ ] **Multi-AZ Failover** — availability-zone redundancy for stateful and stateless tiers
- [ ] **Automated Failover** — failover triggered without manual intervention, and tested

### §19 Cost Management / FinOps
- [ ] **Resource Tagging** — cost-allocation tags applied consistently across resources
- [ ] **Dev Environment Shutdown Policy** — non-prod environments stopped when idle
- [ ] **Budget Alerts** — spend thresholds with alerting wired to an owner
- [ ] **Cost-Aware Architecture Review** — cost considered as a first-class design input

---

## Pre-loaded anti-pattern checklist (floor, not ceiling — extend it)

Apply each explicitly: pass / fail / N-A with a file:line evidence pointer. (See `00-common-core.md` §7.) Evidence here lives in pipeline YAML, IaC modules, deployment manifests, scripts, runbook docs, and dashboard/alert definitions — cite those files:line, never a prose claim that something "is done."

### DevOps & Operations
1. **CI is not a merge gate** — the pipeline builds but doesn't *block* merge on failing unit + integration + architecture/fitness-function tests; broken `main` is possible. *(WAF Operational Excellence — automated, gated deployments; Twelve-Factor — Build/Release/Run separation.)* **Cross-session note:** the architecture/fitness-function test gate overlaps session 01 (do they exist) and session 09 (are they enforced) — assess *whether CI runs them as a gate*, defer their content quality to those sessions.
2. **No deployment strategy beyond "push"** — release is a manual artifact copy or an unguarded `azd up` / `kubectl apply`; no staged, repeatable, reversible mechanism. *(WAF Operational Excellence — safe deployment practices; Twelve-Factor — Release as an immutable, versioned unit.)*
3. **Progressive delivery absent where it belongs** — no canary, blue-green, or traffic-split rollout for a system that advertises Cloud Run revisions / AKS / Container Apps; 100% of traffic cuts over at once with no automated metric-based abort. *(WAF Operational Excellence / Google Cloud Architecture Framework Operational Excellence — progressive exposure and rollback; cross-check Cloud Run revision traffic-splitting in session 12 territory.)*
4. **Rollback path undefined** — there is a way to deploy but no defined, tested way to *un*-deploy a bad release; "rollback" means re-running the forward pipeline with an old tag and hoping migrations are reversible. *(WAF Operational Excellence — rollback as a designed capability; paraphrase if no specific section fits.)*
5. **Pipeline-as-prose, not pipeline-as-code** — deployment steps documented in a README/wiki rather than codified; build/release not reproducible from source. *(Twelve-Factor — Build/Release/Run; dev/prod parity.)*
6. **Dev/prod parity broken** — environments diverge in config source, backing-service versions, or topology such that "works in dev" carries no signal for prod. *(Twelve-Factor — Dev/prod parity factor.)*
7. **SLO/SLI undefined or decorative** — objectives are stated in a doc but not tied to a real, queried metric (the latency/availability indicator isn't actually computed from telemetry). *(Google Cloud Architecture Framework Reliability — define SLIs/SLOs from real signals; paraphrase the SRE error-budget framing.)* **Cross-session note:** SLO/SLI definition overlaps session 05 (where the SLI metrics are emitted) and session 09 (observability/alerting wiring) — assess *governance and existence* here; defer metric-instrumentation quality there.
8. **No error-budget governance** — even where SLOs exist, no error budget is derived from them and nothing changes when it's burned (release freezes, prioritization). *(Google Cloud Architecture Framework Reliability — error-budget policy; paraphrase SRE practice.)*
9. **No on-call / incident ownership** — no defined rotation, escalation path, or single owner for production incidents; "whoever notices" is the model. *(WAF Operational Excellence — incident-response readiness; paraphrase — not on the Tier-1 shelf.)*
10. **No blameless-postmortem discipline** — incidents (or, in a sandbox, the *mechanism* for them) produce no durable, blameless learning artifact; the same failure can recur unexamined. *(WAF Operational Excellence — learn from operational failures; paraphrase.)*
11. **DORA-blind delivery** — no visibility into deployment frequency, lead time for change, change-failure rate, or MTTR, so delivery health is unmeasured and uninspectable. *(DORA / Accelerate capabilities — the four key metrics; paraphrase, not on the Tier-1 shelf.)*
12. **Disposability ignored in the deploy unit** — processes/containers aren't designed for fast startup and graceful shutdown, so rolling deploys and autoscaling can't be done safely. *(Twelve-Factor — Disposability.)*

### DR & BCP
1. **Backups exist but are never restore-tested** — the classic trap: an automated backup with no scheduled restore drill is not a backup, it's an untested hope. *(WAF Reliability — test your recovery procedures; Google Cloud Architecture Framework Reliability — validate backups by restoring.)*
2. **RTO / RPO undefined** — no stated recovery-time and recovery-point objectives, so backup cadence and failover design are unanchored and unverifiable. *(WAF Reliability — define RTO/RPO targets; Google Cloud Architecture Framework Reliability — recovery objectives.)*
3. **Backup cadence vs RPO mismatch** — even where RPO is stated, backup frequency can't actually meet it (e.g. daily backup against a 1-hour RPO claim). *(WAF Reliability — align backup strategy to RPO.)*
4. **DR runbook absent or not version-controlled** — recovery is tribal knowledge or lives outside the repo; no executable, reviewed procedure to follow under pressure. *(WAF Operational Excellence — runbooks as version-controlled operational assets; paraphrase.)*
5. **Multi-AZ / zone redundancy absent or untested** — single-AZ stateful tier (DB, cache) or a failover capability that has never been exercised. *(WAF Reliability — design for redundancy across availability zones; Google Cloud Architecture Framework Reliability — eliminate single points of failure.)*
6. **Automated failover absent or unverified** — failover requires a human in the loop, or the automation exists but has never been triggered in a drill, so its real RTO is unknown. *(WAF Reliability — automate and test failover.)*
7. **Backup scope incomplete** — database is backed up but secrets/keys, configuration, blob/object data, or message-broker state are not, so a "restore" yields a non-functional system. *(WAF Reliability — back up all state required to recover, paraphrase.)*
8. **Backups not protected / not retained correctly** — backups unencrypted, co-located with the primary (shared blast radius), or with no retention/immutability policy against ransomware or accidental deletion. *(WAF Reliability + Security — protect and isolate backups; paraphrase.)*
9. **No tested recovery for non-DB state** — outbox/inbox tables, idempotency stores, or projection read-models have no rebuild/recovery story after a restore, leaving the system inconsistent. *(Cross-link to session 04 messaging; cite Hohpe & Woolf — Guaranteed Delivery / message-store recovery where relevant.)*
10. **DR never flow-tested end to end** — individual pieces (backup, IaC, failover) may exist but were never exercised as one continuous "rebuild from nothing" drill, so unknown gaps remain. *(WAF Reliability — full recovery testing, not component testing.)*

### Cost Management / FinOps
1. **Resource tagging / cost-allocation absent** — resources carry no owner/env/cost-center tags, so spend can't be attributed or governed. *(WAF Cost Optimization — tag resources for cost allocation; Google Cloud Architecture Framework Cost Optimization — labels for attribution.)*
2. **No dev-environment shutdown policy** — non-prod compute runs 24/7, burning idle spend nights and weekends with no auto-stop/auto-start schedule. *(WAF Cost Optimization — shut down or deallocate non-production resources when idle.)*
3. **Budget alerts absent** — no spend thresholds, no anomaly/budget alerting wired to an owner, so cost overruns are discovered on the invoice. *(WAF Cost Optimization — set budgets and alerts; Google Cloud Architecture Framework Cost Optimization — budgets and billing alerts.)*
4. **Cost-aware architecture review never performed** — cost is never a design input; SKU/tier choices, always-on services, and data-egress patterns are picked with no cost lens. *(WAF Cost Optimization — cost as a design constraint; paraphrase.)*
5. **Over-provisioned by default** — fixed, oversized SKUs/replicas with no right-sizing or scale-to-zero where the platform supports it (e.g. Cloud Run / Container Apps idle scale-down). *(WAF Cost Optimization — right-size and use elasticity; cross-check autoscaling in session 11 / 12 territory.)*
6. **No cost visibility per environment / mode** — tri-mode (Local/Azure/GCP) spend isn't separable, so the cost of each cloud path is unknown and uncomparable. *(Google Cloud Architecture Framework Cost Optimization — cost monitoring and attribution; paraphrase.)*
7. **Egress / inter-zone traffic cost ignored** — chatty cross-zone or cross-region calls and unbatched data movement incur silent network cost with no review. *(WAF Cost Optimization — account for data-transfer cost; paraphrase.)*
8. **No retention/lifecycle policy on cost-bearing stores** — logs, backups, blobs, and metrics accumulate indefinitely with no tiering or expiry, so storage cost grows unbounded. *(WAF Cost Optimization — manage data lifecycle to control storage cost.)* **Cross-session note:** data-retention policy is governance-owned in session 08 (Compliance) — assess the *cost* angle here, defer the compliance/retention-policy angle there.

---

## Mini flow-traces for this session (Method 3, scoped)

Trace at the **delivery- and operations-altitude**, not for business correctness. Each discontinuity ("this is where the gate is missing" / "this step has never run") is an `OPS` finding.

- **Commit → CI → artifact → deploy (the gated-delivery trace):** start at a commit and follow the pipeline definition end to end. *What gates exist between a developer's push and production?* Specifically: does a failing unit/integration/architecture test *block* merge, or merely report? Is the artifact built once and promoted, or rebuilt per environment (Build/Release/Run separation)? Is there an approval, a canary, a metric check, before 100% of traffic moves? And the key question — **what can reach production ungated?** Any path that bypasses the gates (manual deploy, console change, un-reviewed IaC apply) is the finding.
- **Restore-from-backup (the recovery trace) — only if a runbook exists:** find the DR/restore runbook and trace it as if executing it cold. *Does it actually work end to end* — backup → provision target → restore data → restore secrets/config → re-point the app → verify? Is it version-controlled in the repo? Is it run on a schedule, or written once and never exercised? If no runbook exists at all, that absence is itself the primary DR finding (audit by omission), and the trace becomes "there is nothing to trace."

---

## Primary anchors for this session

This cluster is **mostly Tier-2 territory** — lead with the cloud frameworks and the operational-excellence canon, and follow the citation-preference rule (paraphrase rather than over-cite where the shelf is thin):

- **Azure Well-Architected Framework — Operational Excellence, Reliability, and Cost Optimization pillars** — cite the *specific pillar* (Operational Excellence for CI/CD, runbooks, deployment safety; Reliability for RTO/RPO, redundancy, recovery testing; Cost Optimization for tagging, budgets, right-sizing).
- **Google Cloud Architecture Framework — Operational Excellence, Reliability, and Cost Optimization pillars** — the GCP-side equivalents; lead here for the GCP path of the tri-mode deployment (Cloud Run revisions, traffic splitting, budgets/labels).
- **The Twelve-Factor App** — Build/Release/Run separation, dev/prod parity, Disposability — the backbone for the CI/CD and deployment-unit findings.
- **DORA / Accelerate capabilities** — deployment frequency, lead time for change, change-failure rate, MTTR — the delivery-health metrics. *Paraphrase; not on the Tier-1 shelf.*
- **SRE error-budget practice** — SLO → error budget → release governance. *Paraphrase; not on the Tier-1 shelf.*

**Reference implementations to compare against:** the CI/CD + IaC shape of **eShop** (GitHub Actions / azd pipelines), the **Jason Taylor / Ardalis Clean Architecture template** CI workflow (build + test gating), and canonical **azd / Bicep / Terraform** deployment-pipeline samples for the tri-mode (Local/Azure/GCP) story. For each big-ticket operational pattern present in WMS (gated CI, deployment strategy, backup/restore, budgets), name the reference equivalent and label the difference **intentional**, **drift**, or **missing**.

---

## ADR touchpoints to verify (Method 1)

Open every ADR touching: **CI/CD pipeline** design and gating · **deployment / release strategy** (canary, blue-green, progressive delivery, rollback) · **SLO / error-budget policy** · **backup & DR strategy** (RTO/RPO, restore testing, failover) · **cost-governance / FinOps** (tagging, budgets, environment-shutdown). For each: *claimed decision | code reality | faithful / drifted / silent / missing | evidence (file:line)* — and remember the source-of-truth rule: an ADR claiming "backups are restore-tested monthly" with no scheduled job or drill artifact in the repo is a **drifted/missing** finding, default High.

Also surface **silent operational decisions** — a deployment strategy, retention policy, or failover assumption baked into pipeline/IaC with no ADR behind it. Record all ADR observations *and* silent decisions in the partial's **ADR touchpoints** block for session 99 to consolidate into the ADR drift matrix.

---

## Output

Write the partial report following the **partial-report skeleton in `00-common-core.md` §11**, covering §13, §18, and §19, with all findings under the `OPS` prefix and a **scorecard-contribution row for each of the three dimensions** (DevOps & Operations · DR & BCP · Cost / FinOps). Carry forward the cross-session notes flagged above (SLO/SLI ↔ sessions 05 & 09; CI test-gate ↔ sessions 01 & 09; data-retention cost ↔ session 08; health checks ↔ sessions 05 & 06) so synthesis reconciles rather than double-counts. Then stop — the report is the work.
