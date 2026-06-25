# Principal Architect Audit — Session Suite (Index & Run Order)

This folder is the original `principal-architect-audit-prompt.md` mega-prompt **decomposed into focused sessions**. Each session is run in its **own fresh chat** so it gets a full context budget and goes deep, instead of one over-stretched run that skims 14+ dimensions. The hand-off between sessions is the **partial reports on disk**, not chat memory.

## Why split it

A single session that must map the whole repo **and** trace every dimension **and** apply three review methods spends most of its budget on warm-up and breadth, leaving little for depth on any one dimension. Splitting trades a little redundant warm-up per session for a lot more depth per dimension — and lets you run clusters in parallel.

## How to run

1. **Read `00-common-core.md`** — it is the binding contract every session shares (read-only guardrail, role, source-of-truth, review methods, authoritative anchors, verdict format, calibration rubric, partial-report format). You do not run it; each session reads it.
2. For each cluster session (01–10): open a **fresh chat in the WMS repo**, paste the session file's prompt, let it run, and confirm it wrote its **partial report** to `docs/working-docs/audit/partials/`.
3. After the cluster sessions you want are done, run **`99-synthesis.md`** in a fresh chat. It reads all the partials and assembles the single final report at `docs/working-docs/audit/<YYYY-MM-DD>-principal-architect-audit.md`.

> **Report paths are relative to the WMS repo root** (where the audit runs), not to this `audit-prompts/` folder (where the prompts live).

## Session map

| # | Session file | Dimensions covered | Checklist §§ | ID prefix | Depends on |
|---|---|---|---|---|---|
| — | `00-common-core.md` | *Binding contract — read first in every session* | — | — | — |
| 01 | `01-architecture-and-code-quality.md` | Application Architecture · Code Quality | §1, §6 | `ARC` | — |
| 02 | `02-data-persistence-and-performance.md` | Data & Persistence · Performance & Scalability | §2, §11 | `DAT` | — |
| 03 | `03-api-and-integration.md` | API Design · Communication & Integration | §3, §4 | `API` | — |
| 04 | `04-security-and-compliance.md` | Security & Auth · Compliance & Data Governance · (Multi-Tenancy isolation surface) | §7, §17, §16 | `SEC` | — |
| 05 | `05-resilience-and-observability.md` | Resilience · Observability | §8, §9 | `RES` | — |
| 06 | `06-infrastructure-and-platform.md` | Infrastructure & Cloud · Cross-Cutting Concerns | §12, §10 | `INF` | — |
| 07 | `07-devops-operations-dr-finops.md` | DevOps & Operations · DR & BCP · Cost / FinOps | §13, §18, §19 | `OPS` | — |
| 08 | `08-frontend.md` | Frontend & UI | §5 | `FE` | — |
| 09 | `09-testing-and-qa.md` | Testing / QA architecture (cross-lens) | §14 | `QA` | — |
| 10 | `10-flow-tracing.md` | End-to-end flow tracing (cross-cutting) | — | `FLOW` | benefits from 01–03 |
| 99 | `99-synthesis.md` | Final report assembly | all | `SYN` | **all partials** |

**17 active dimensions → 9 thematic cluster sessions + 1 flow-tracing + 1 synthesis = 11 session runs.** Not one-per-dimension (that repeats repo warm-up 17×); not one-shot (that goes shallow). The clusters group dimensions that share code-surface and authoritative anchors.

## Run order

- **01–10 are mutually independent** — run in any order, or in parallel across separate chats. Each writes its own partial; none reads another's output.
- **10 (flow-tracing)** is independent but *benefits* from running after 01–03, because by then you understand the architecture, persistence, and messaging spine it traces. Running it cold is allowed (it builds its own map) but slower.
- **99 (synthesis) runs last** — it requires the partials it will consolidate to already exist. If you skip some cluster sessions, synthesis records those dimensions under "Not audited" rather than inventing verdicts.

## Two cross-lenses — where they live

The original named two cross-lenses. They are handled like this:
- **Testing / QA architecture** → its own session, **09**.
- **Decision / ADR fidelity** → **distributed**: every cluster session records *ADR touchpoints* for ADRs in its scope (see `00-common-core.md` §6 Method 1 and §11), and **session 99 consolidates them into the ADR drift matrix**. There is no separate ADR session.

## Not-yet-planned dimensions

The checklist marks two sections **"Belum direncanakan"** (not yet planned):
- **§15 AI / LLM Integration** — no dedicated session. If any LLM-integration code exists despite the "not planned" status, whichever session finds it (likely 03 or 06) flags it as a **silent decision**. Otherwise session 99 records it under "Not audited — not yet planned (intentional scope exclusion)".
- **§16 Multi-Tenancy** — no dedicated session, **but** it has real surface in security (warehouse-scoping data isolation, row-level security). Session **04** checks whether tenancy isolation exists; nascent multi-tenancy code despite "not planned" is a silent-decision finding. Session 99 records the formal-dimension status as "Not audited — not yet planned".

## Output layout (inside the WMS repo)

```
docs/working-docs/audit/
├─ partials/
│  ├─ <YYYY-MM-DD>-01-architecture-and-code-quality.md   (ARC)
│  ├─ <YYYY-MM-DD>-02-data-persistence-and-performance.md (DAT)
│  ├─ ...                                                  (API, SEC, RES, INF, OPS, FE, QA)
│  └─ <YYYY-MM-DD>-10-flow-tracing.md                      (FLOW)
└─ <YYYY-MM-DD>-principal-architect-audit.md              (final — written by session 99)
```

## The final report (session 99 output)

Session 99 produces the same 8-part report the original mega-prompt specified, assembled from the partials:
1. Executive summary (≤ 1 page)
2. Scorecard heat-map (all active dimensions + 2 cross-lenses, R/A/G + maturity 1–5)
3. Top critical findings (max 10)
4. ADR drift matrix
5. Per-dimension deep dive (assembled from partials)
6. Audit-by-omission — consolidated
7. Proposed ADRs (inline)
8. Not-audited register
