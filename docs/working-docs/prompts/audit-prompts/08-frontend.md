# Session 08 — Frontend & UI

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **dimension-specific scrutiny** for this session.

- **Session code / partial filename:** `08-frontend`
- **Finding ID prefix:** `FE` (e.g. `FE-001`)
- **Checklist sections covered:** §5 Frontend & UI (from `tech-checklist-blank.md`)
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-08-frontend.md` (WMS repo root)

This is the **client-surface** session. The frontend stack — Blazor (Server / WebAssembly / Auto), MudBlazor, SignalR, Fluxor — is a *distinct technology surface* from the otherwise backend-shaped audit: its failure modes (render-mode choice, circuit-scoped service lifetimes, server-held UI state, re-render discipline, component disposal) do not show up anywhere in the persistence, messaging, or resilience sessions, so it earns a focused session of its own rather than being diluted into an unrelated cluster. Most of the lead anchors here are Tier 2 (Microsoft Learn for Blazor; MudBlazor and Fluxor docs) with **Newman** carrying the one genuinely Tier-1 concern — a micro-frontend is a *module boundary*, and the decomposition rules are the same. Two cross-session seams live here: SignalR scale-out (Redis backplane) overlaps session 06, and client-side-vs-server-side authorization overlaps session 04 — flag, don't double-count.

---

## Scope — checklist items to verdict

### §5 Frontend & UI
- [ ] **Blazor Web App (.NET 8)** — hosting model, render-mode strategy, component architecture
- [ ] **MudBlazor** — component-library usage idioms, theming, form/validation patterns
- [ ] **Scoped services** — service lifetimes across a Blazor Server circuit; DbContext/scoped-state hazards
- [ ] **SignalR** — real-time hub usage, circuit/connection lifecycle, scale-out posture
- [ ] **Blazor WebAssembly / Auto render mode** — render-mode selection rationale, prerender, interactivity boundary
- [ ] **Fluxor** — unidirectional state management, store/reducer/effect discipline vs ad-hoc component state
- [ ] **Micro-Frontend Architecture** — module-boundary reality vs premature decomposition
- [ ] **A/B Testing Infrastructure** — experiment plumbing, variant assignment, measurement

---

## Pre-loaded anti-pattern checklist (floor, not ceiling — extend it)

Apply each explicitly: pass / fail / N-A with a file:line evidence pointer. (See `00-common-core.md` §7.)

### Render model & hosting (Blazor Web App)
1. **Render mode left at default with no rationale** — Server vs WebAssembly vs Auto chosen by inertia (or the template default) rather than a deliberate trade-off between latency, offline capability, server cost, and circuit footprint; no ADR explaining the call. *(Microsoft Learn — ASP.NET Core Blazor render modes; the choice is architectural, not cosmetic.)*
2. **Interactivity boundary set too coarse** — interactivity applied at the whole-app/root level when most of the tree is static, forcing needless circuits/WASM payload; or `InteractiveServer`/`InteractiveWebAssembly` sprinkled per-component with no coherent strategy. *(Microsoft Learn — Blazor render modes, per-page/per-component interactivity.)*
3. **Prerender not accounted for** — component logic runs twice (prerender + interactive reconnect) and double-fires side effects, or fetches data twice, because `OnInitializedAsync` is not written to be prerender-safe. *(Microsoft Learn — prerendering and `PersistentComponentState`.)*
4. **WASM secrets / config bleed** — anything sensitive shipped into the WebAssembly bundle (it is fully visible to the client) instead of being kept server-side or behind an authenticated API. *(The Twelve-Factor App — Config; cross-link session 04 — nothing trusted ships to the client.)*

### Service lifetimes in the circuit (Blazor Server)
5. **DbContext captured across a long-lived circuit** — a scoped `DbContext` (or other scoped service) resolved once and reused for the lifetime of a Server circuit, which can span minutes/hours, yielding stale tracking, concurrency conflicts, and disposal-timing hazards. *(Microsoft Learn — DI service lifetimes in Blazor Server; `DbContextFactory` / `OwningComponentBase` is the idiom.)*
6. **Singleton capturing scoped/per-user state** — a singleton service holding data that is really per-circuit/per-user, leaking one user's state into another's session. *(Microsoft Learn — service scope in Blazor; cross-link session 04 on tenant/user isolation.)*
7. **Shared mutable state on a Server-side service used concurrently** — circuit re-entrancy or parallel events mutating shared service state without synchronization. *(Microsoft Learn — threading/`InvokeAsync` and circuit concurrency; paraphrase if unsure.)*

### State management (Fluxor) & re-render discipline
8. **Ad-hoc mutable component state where a store belongs** — cross-cutting application state mutated directly in components instead of flowing through Fluxor's unidirectional store → action → reducer cycle, so state changes can't be reasoned about or traced. *(Fluxor docs — unidirectional data flow; Freeman & Robson — Observer wired so subscribers can't be reasoned about, as the underlying pattern smell.)*
9. **Impure reducers / side effects in the wrong place** — reducers performing I/O, mutation, or async work instead of being pure `(state, action) → state`; side effects not isolated in Effects. *(Fluxor docs — reducers are pure, effects own side effects.)*
10. **Unnecessary re-renders / `StateHasChanged` misuse** — `StateHasChanged()` called defensively/everywhere, `ShouldRender` never overridden on hot components, or large subtrees re-rendering on every keystroke. *(Microsoft Learn — Blazor performance best practices: `ShouldRender`, virtualization, avoiding needless renders.)*
11. **Direct parent-state mutation instead of `EventCallback`** — child components reaching up and mutating parent fields rather than raising an `EventCallback`, breaking the one-way data-flow contract and the render lifecycle. *(Microsoft Learn — Blazor `EventCallback` and component data flow.)*

### Component lifecycle, disposal & library usage
12. **Undisposed subscriptions / timers / `IDisposable`** — Fluxor/SignalR/observable subscriptions, `System.Timers.Timer`, `CancellationTokenSource`, or other `IDisposable` created in components but no `IDisposable`/`IAsyncDisposable` implemented, leaking across the long-lived circuit. *(Microsoft Learn — component disposal and `IAsyncDisposable`; Release It! resource-leak smell, paraphrase.)*
13. **MudBlazor used against the grain** — re-rolling form/validation, dialogs, or data grids by hand where MudBlazor provides the idiom (`MudForm`, `MudDialog`, `MudDataGrid`, `EditForm` integration), or theming via inline styles instead of the `MudThemeProvider`. *(MudBlazor documentation — component usage patterns.)*
14. **SignalR circuit/state held server-side with no scale-out plan** — Blazor Server circuits and/or a custom hub keep per-connection state on one node with no Redis backplane, so a second instance or a reconnect to a different node silently loses state. *(Microsoft Learn — SignalR scale-out / Redis backplane; **cross-link session 06**.)*
15. **Micro-frontend boundary premature or fake** — a "micro-frontend" split that isn't an independently deployable/ownable module (shared mutable state, shared build, no contract at the seam) — decomposition cost paid with none of the benefit. *(Newman, *Building Microservices* — module boundaries and the cost of premature decomposition apply identically to front-end modules.)*
16. **Authorization enforced only by hiding UI** — buttons/menus hidden via `AuthorizeView` but the underlying API/handler not independently authorized, so the action is reachable by anyone who calls the endpoint. *(OWASP Top 10 — Broken Access Control; **cross-link session 04** — UI hiding is presentation, not enforcement.)*
17. **A/B testing as scattered `if` branches** — variant logic hardcoded in components with no assignment/measurement infrastructure, so experiments can't be reasoned about, turned off, or measured. *(Paraphrase — flag the absence of real experiment plumbing rather than citing authority.)*

---

## Mini flow-traces for this session (Method 3, scoped)

Trace at the **client-architecture altitude** — how state, lifetime, and rendering behave, not business correctness:

- **One interactive Blazor Server component, full lifecycle:** mount → `OnInitializedAsync` (prerender + interactive) → first render → an interactive event → re-render → navigate away/disconnect → dispose. *Watch:* which service lifetimes are resolved and **where state actually lives** (component field? Fluxor store? captured scoped service?); is a `DbContext` captured for the circuit's life or created per-operation via a factory; are subscriptions/timers/`IDisposable` actually disposed; does `OnInitializedAsync` double-fire across prerender?
- **One SignalR-backed live-update path, edge to edge:** server event (or hub method) → SignalR transport → client handler → state update → re-render. *Watch:* where the connection/circuit state lives, what happens on reconnect or on a second server instance (backplane present or not — cross-link session 06), whether the client handler marshals back onto the render context (`InvokeAsync(StateHasChanged)`), and whether the subscription is disposed with the component.

Mark every discontinuity ("this is where the lifecycle/state model breaks down") as an `FE` finding.

---

## Primary anchors for this session

Lead with these (mostly Tier 2 per `00-common-core.md` §8, with Newman as the Tier-1 boundary anchor):
- **Microsoft Learn — ASP.NET Core Blazor** — render modes (Server / WebAssembly / Auto), component lifecycle, prerendering & `PersistentComponentState`, DI service lifetimes in Server circuits, performance best practices (`ShouldRender`, virtualization), `EventCallback`, component disposal. *Lead for render-model, lifetime, lifecycle, and re-render findings.*
- **Microsoft Learn — ASP.NET Core SignalR** — hub lifecycle, scale-out, Redis backplane. *Lead for real-time and scale-out posture (with session 06 on the backplane).*
- **MudBlazor documentation** — component usage idioms, forms/validation, dialogs, data grid, theming. *Lead for component-library idiom findings.*
- **Fluxor documentation** — store/action/reducer/effect model, unidirectional flow, reducer purity. *Lead for state-management discipline findings.*
- **The Twelve-Factor App — Config** — for the SPA/backend config boundary and keeping secrets out of the client bundle.
- **Newman, *Building Microservices* — module boundaries / cost of premature decomposition** — the one Tier-1 lead, for the micro-frontend boundary verdict.

Reference implementations to compare structure against: **eShop** (its Blazor client + render-mode and state choices) and the **MudBlazor sample/admin templates** for idiomatic component usage. Name the reference equivalent for each big-ticket choice (render mode, state management, real-time) and label each substantive difference **intentional / drift / missing**.

---

## ADR touchpoints to verify (Method 1)

Open every ADR touching: **Blazor hosting / render model** (why Server vs WebAssembly vs Auto, and the interactivity boundary), **state management** (the Fluxor decision — why a store, what state belongs in it), **real-time strategy** (SignalR — what is pushed, and the scale-out/backplane stance), and the **micro-frontend decision** (split or deliberately not). For each: *claimed decision | code reality | faithful / drifted / silent / missing | evidence (file:line)*. The render-mode choice especially tends to be a **silent decision** baked into `App.razor`/`_Imports`/component attributes with no ADR — surface it and record it. Put all of these in the partial's **ADR touchpoints** block for session 99 to consolidate into the drift matrix.

---

## Output

Write the partial report following the **partial-report skeleton in `00-common-core.md` §11**, covering §5 Frontend & UI, with all findings under the `FE` prefix and a scorecard-contribution row for the dimension. Record the two cross-session seams (SignalR Redis backplane → session 06; client-side vs server-side authorization → session 04) in the **Cross-session notes** block so synthesis reconciles rather than double-counts. Then stop — the report is the work.
