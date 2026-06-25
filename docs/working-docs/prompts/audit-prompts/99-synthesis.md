# Session 99 — Synthesis & Final Report Assembly

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the report conventions. This session is **structurally different** from 01–10: it renders no new dimension audit. It consumes the partial reports and assembles the single final report. Where this file and common-core appear to differ on *what to produce*, this file governs (it defines the final-report shape); on the *contract* (read-only guardrail, verdict format, citation rules, rubric) common-core wins as always.

- **Session code:** `99-synthesis`
- **Role:** **consolidator, not auditor** — you assemble, de-duplicate, and reconcile; you do not open fresh dimensions or invent verdicts.
- **Inputs:** `docs/working-docs/audit/partials/*.md` (every partial written by sessions 01–10)
- **Output:** `docs/working-docs/audit/<YYYY-MM-DD>-principal-architect-audit.md` (WMS repo root)

This session renders **no new dimension audit**. It reads every partial report on disk, **de-duplicates** findings that recur across partials, **reconciles** cross-session conflicts, and **assembles** the final report. It is **read-only with respect to source**: unlike the cluster sessions it does not map the repo or re-audit any dimension. It **MAY re-open a specific code location only to break a tie** when two partials disagree on a shared concern (§Reconciliation rules, rule 3) — never to re-audit, extend coverage, or generate findings the partials did not already raise. It **runs last**: if some partials are missing (a session was skipped), it records those dimensions under **"Not audited — session not run"** in the Not-audited register rather than inventing verdicts to fill the grid. Confidence-padding the heat-map with guessed levels is the same contract violation here as anywhere (`00-common-core.md` §12).

---

## Inputs

Read **every** file in `docs/working-docs/audit/partials/`. Do not assume the set — list the directory and consume what is actually there. The expected mapping (mirror of the `00-index.md` session map) is below; a partial may be absent (session skipped) — note absences, do not fabricate their content.

| Partial (session code) | Dimensions it carries | Finding ID prefix |
|---|---|---|
| `01-architecture-and-code-quality` | Application Architecture · Code Quality | `ARC` |
| `02-data-persistence-and-performance` | Data & Persistence · Performance & Scalability | `DAT` |
| `03-api-and-integration` | API Design · Communication & Integration | `API` |
| `04-security-and-compliance` | Security & Auth · Compliance & Data Governance · (Multi-Tenancy isolation surface) | `SEC` |
| `05-resilience-and-observability` | Resilience · Observability | `RES` |
| `06-infrastructure-and-platform` | Infrastructure & Cloud · Cross-Cutting Concerns | `INF` |
| `07-devops-operations-dr-finops` | DevOps & Operations · DR & BCP · Cost / FinOps | `OPS` |
| `08-frontend` | Frontend & UI | `FE` |
| `09-testing-and-qa` | Testing / QA architecture (cross-lens) | `QA` |
| `10-flow-tracing` | End-to-end flow tracing (cross-cutting) | `FLOW` |

From each partial, harvest its five structural blocks for downstream assembly: **Findings** (verdict format), **ADR touchpoints**, **Audit-by-omission**, **Scorecard contribution**, **Cross-session notes**, and **Not audited (this scope)**.

**Preserve every finding ID exactly as the partial assigned it** (`ARC-007`, `SEC-014`, `FLOW-003`, …). **Do not renumber, re-prefix, or re-sequence.** The IDs are the cross-reference fabric of the final report — the Top-critical list, the ADR drift matrix, and the Proposed-ADRs section all link back to deep-dive entries *by ID*. Renumbering breaks every link and destroys traceability back to the originating partial.

---

## Reconciliation rules

Before assembling, resolve overlaps and conflicts. The partials were written in independent fresh chats; the same concern legitimately surfaces in several of them. Your job is to render **one** verdict per concern without losing evidence.

1. **De-duplicate recurring findings.** When the *same* concern appears in multiple partials, keep the **most specific** instantiation (the one with the sharpest file:line evidence and the most precise canonical alternative) as the canonical entry, and **cross-reference** the rest to it by ID rather than restating them. Do not silently drop the duplicates — a one-line "also raised as `DAT-006`, `RES-009`" preserves that multiple lenses independently caught it (which is itself signal about severity). Never sum duplicates into an inflated finding count.

2. **Resolve known cross-session overlaps into ONE verdict, using the partials' "Cross-session notes" blocks.** These concerns span dimensions by construction; each owning partial saw only its slice. Reconcile each into a single verdict and **note the reconciliation** (which partials contributed, which lens owns the final verdict):
   - **Idempotency** — across **03** (API/mutating endpoints), **04** (security replay surface), **05** (retry safety / at-least-once delivery). One verdict on idempotency posture.
   - **Correlation ID propagation** — across **03** (inbound/edge) and **05** (across the async/messaging boundary). One verdict on end-to-end trace continuity.
   - **SLO / SLI definition** — across **05** (metrics → SLI), **07** (operational targets / error budgets), **09** (are they tested/governed). One verdict.
   - **Health checks (readiness vs liveness)** — across **05** (the probe split itself), **06** (infra/orchestrator wiring), **07** (deployment gating). One verdict.
   - **Redis backplane** — across **06** (infra provisioning / config) and **08** (SignalR / frontend scale-out dependency). One verdict.
   - **Fitness functions / architecture tests** — across **01** (the rules to enforce) and **09** (whether they exist and run in CI). One verdict.
   When the contributing partials *agree*, merge cleanly and cite both IDs. When they *emphasise different facets*, state the unified verdict and list the facets under it.

3. **When two partials disagree on a shared concern, re-open the code to break the tie.** This is the **only** circumstance in which this session reads source. Open the exact location(s) the disagreeing partials cite, determine the real behaviour on the code's own merits (`00-common-core.md` §3 — code is the only source of truth), record the **evidence (file:line)** you used to adjudicate, and state which partial's verdict stands and why. Do not expand the re-read beyond the disputed location into a fresh audit.

> Severity and maturity remain governed by the rubric in `00-common-core.md` §10 — apply it for consistency when a merged finding inherits divergent severities from its sources (take the higher, justify in one line).

---

## Final report structure (the only file you write)

Assemble the eight parts below — the same eight the original mega-prompt's **Deliverables** specified — into one document at the Output path. Each part carries a one-line note on **how to assemble it from the partials**; the section *content* still obeys the verdict format and conventions in `00-common-core.md`.

1. **Executive summary** (≤ 1 page) — overall posture, top 3–5 risks, top 3 things WMS gets right.
   *Assemble:* synthesise from the partials' scorecard contributions and top findings; written last, after parts 2–3 exist. No new judgement beyond what the partials support.

2. **Scorecard heat-map** — all **17 active dimensions** + the **2 cross-lenses** (Testing/QA from session 09; ADR fidelity from the consolidated ADR touchpoints), each **R/A/G + maturity 1–5 + one-line**. Mark **§15 AI/LLM Integration** and **§16 Multi-Tenancy** as **"Not planned"** (not R/A/G).
   *Assemble:* stitch the per-dimension scorecard-contribution rows from every partial into one table; the ADR-fidelity row is derived from the consolidated drift matrix (part 4), not from any single partial. Any dimension whose partial is missing → row reads "Not audited — session not run", not a guessed level.

3. **Top critical findings** (max 10) — across all partials, in priority order, each **linked to its deep-dive entry** (part 5) by ID.
   *Assemble:* rank the Critical/High findings from all partials by the §10 severity rubric; on ties, prefer the one that sets a wrong learning anchor (sandbox priority). Link each by its preserved ID; do not restate the full verdict here.

4. **ADR drift matrix** — consolidated: **ADR | claimed decision | code reality | faithful / drifted / silent / missing | severity | evidence (file:line)**.
   *Assemble:* union every partial's **ADR touchpoints** block, keyed by ADR number. Where multiple partials touched the same ADR, merge their rows into one verdict (apply §Reconciliation rule 1). Silent decisions surfaced by any partial become "silent" rows.

5. **Per-dimension deep dive** — each dimension in turn: maturity + justification → checklist applied → flow-trace observations → all findings (positive ✓ and negative).
   *Assemble:* **integrate and dedupe** from the partials — **do NOT rewrite findings from scratch**. Lift each partial's per-dimension section, apply the reconciliation rules, and **preserve IDs**. This is editing and stitching, not re-auditing.

6. **Audit-by-omission — consolidated** — what's missing across the whole system, grouped by impact.
   *Assemble:* union of every partial's **Audit-by-omission** block; collapse the same gap reported by several partials into one entry (cross-reference the rest); regroup by impact so the gap *shape* is visible at a glance rather than scattered per dimension.

7. **Proposed ADRs** — written **inline within this report**. For each: ADR number/title, decision, context, consequences, **motivating finding IDs**.
   *Assemble:* draw from the motivating findings across all partials (especially silent decisions and high-severity omissions). These are *proposals inside the report* — you do **not** create ADR files (see Output).

8. **Not-audited register** — union of every partial's **"Not audited (this scope)"** + any **skipped sessions** (record the skipped session's dimensions as "Not audited — session not run") + the **two not-planned dimensions** (§15 AI/LLM, §16 Multi-Tenancy — "Not audited — not yet planned (intentional scope exclusion)"). Coverage honesty over coverage breadth (`00-common-core.md` §12).

---

## Output

Write the single final report to `docs/working-docs/audit/<YYYY-MM-DD>-principal-architect-audit.md` (WMS repo root; create `docs/working-docs/audit/` if absent). **This is the only file this session writes.** It touches **no** source, configuration, infrastructure, migration, or ADR file — the **Proposed ADRs (part 7) are written inline within the report, not as ADR files**. Re-opening code is permitted *only* to break a tie under §Reconciliation rule 3, and even then nothing is written back. Then stop — the assembled report is the work.
