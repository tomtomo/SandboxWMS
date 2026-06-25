# Session 06 — Infrastructure & Platform

> **Read `00-common-core.md` first — it is binding.** It carries the read-only guardrail, role, source-of-truth rule, the three review methods, the authoritative anchors, the verdict format, the calibration rubric, and the partial-report format. This file adds only the **scope** and **dimension-specific scrutiny** for this session.

- **Session code / partial filename:** `06-infrastructure-and-platform`
- **Finding ID prefix:** `INF` (e.g. `INF-001`)
- **Checklist sections covered:** §12 Infrastructure & Cloud · §10 Cross-Cutting Concerns (from `tech-checklist-blank.md`)
- **Output:** one partial report at `docs/working-docs/audit/partials/<YYYY-MM-DD>-06-infrastructure-and-platform.md` (WMS repo root)

This is the **platform-topology** session: how the system is *provisioned, configured, and wired* below the application code. Infrastructure (containers, IaC, registries, clusters, orchestration) and the platform-level cross-cutting concerns (configuration, gateway, service discovery, mesh) are clustered because together they tell the **tri-mode Local / Azure / GCP** story — the same code must stand up faithfully in three provisioning worlds. The decisive question across both dimensions is whether the *abstractions are real*: does a Twelve-Factor config boundary or a Ports-&-Adapters cloud port genuinely isolate the application from provider detail, or does provider-specific concretion leak through? This dimension is mostly **Tier-2 territory** (WAF, Google Cloud Architecture Framework, Twelve-Factor), so lean on those pillars by name and reserve Tier 1 for the Ports-&-Adapters *execution* check.

---

## Scope — checklist items to verdict

### §12 Infrastructure & Cloud
- [ ] **Docker** — Dockerfile hygiene, multi-stage build, image provenance
- [ ] **IaC** — Bicep + Terraform authored, parameterised, reviewed
- [ ] **Environment Provisioning** — repeatable, idempotent stand-up of an environment
- [ ] **Session Affinity (sticky session)** — sticky vs stateless workload design
- [ ] **Cloud Adapter Pattern (Ports & Adapters)** — provider-swappable across Local/Azure/GCP
- [ ] **Container Registry** — image push/pull, tagging, provenance
- [ ] **Serverless Container Compute** — managed container runtime (Container Apps / Cloud Run)
- [ ] **One-shot Container Job** — run-to-completion job semantics (migrations, seeders)
- [ ] **Cloud Run revisions + traffic splitting** — revisioned rollout, weighted traffic
- [ ] **GitOps (ArgoCD/Flux)** — declarative desired-state reconciliation
- [ ] **AKS mature + Helm Charts** — cluster topology, chart hygiene
- [ ] **Network Policy** — default-deny, intra-cluster segmentation
- [ ] **Multi-Cluster Management** — fleet/multi-cluster strategy
- [ ] **Redis Backplane untuk SignalR** — scale-out backplane for real-time hub

### §10 Cross-Cutting Concerns
- [ ] **Configuration File** — `appsettings*.json` layering, per-environment override
- [ ] **Environment Variables** — config in environment, no secrets in source
- [ ] **Feature Flags** — runtime toggles, kill-switches vs static config
- [ ] **API Gateway** — auth, routing, rate-limiting at the edge vs leaking into services
- [ ] **Centralized Configuration Management** — App Configuration / Runtime Config / Key Vault wiring
- [ ] **Service Discovery** — how services resolve each other per mode
- [ ] **Service Mesh (Istio/Linkerd)** — justified vs cargo-culted

---

## Pre-loaded anti-pattern checklist (floor, not ceiling — extend it)

Apply each explicitly: pass / fail / N-A with a file:line evidence pointer. (See `00-common-core.md` §7.)

### Infrastructure & Cloud
1. **Dockerfile not multi-stage** — build SDK, restore caches, and source tree shipped in the runtime image instead of a slim `aspnet` runtime stage, bloating the attack surface and image size. *(Azure WAF — Security pillar, minimise attack surface; Twelve-Factor X — dev/prod parity via thin, identical artifacts.)*
2. **Container runs as root** — no `USER` directive dropping to a non-root identity; the process runs as UID 0 inside the container. *(Azure WAF — Security pillar, least-privilege workloads; Google Cloud Architecture Framework — Security, run containers as non-root.)*
3. **Base image unpinned or fat** — `latest` tag or a full distro base rather than a pinned digest / `-alpine` / `-chiseled` minimal image, so builds are non-reproducible and CVE surface is large. *(Twelve-Factor II — explicitly declared, isolated dependencies; WAF — Operational Excellence, reproducible builds.)*
4. **Secrets baked into the image** — connection strings, API keys, or certificates `COPY`ed or `ARG`/`ENV`-injected into a layer, recoverable from image history; missing `.dockerignore` leaks `.env`, `appsettings.*.json`, or `.git`. *(Twelve-Factor III — config in the environment, never in the artifact. Cross-session note — secrets management is owned by session 04; here flag only the image-provenance leak.)*
5. **IaC tri-mode parity gap** — a resource provisioned in Bicep for Azure has no Terraform equivalent for GCP (or vice versa), so the three modes silently diverge and "tri-mode" is aspirational, not real. *(Newman — environment definition and dev/prod parity; WAF — Operational Excellence, infrastructure as code.)*
6. **IaC not idempotent / drift-prone** — templates that fail or duplicate on re-apply, out-of-band manual portal changes with no drift detection, or remote state not locked/backed (local `terraform.tfstate`, no backend). *(Google Cloud Architecture Framework — Operational Excellence, manage infrastructure as code; WAF — Operational Excellence, deployment idempotency.)*
7. **Config drift across environments** — environment-specific values hard-coded into committed `appsettings.json` rather than overridden through environment-scoped sources, so Local/Azure/GCP behave differently for non-obvious reasons. *(Twelve-Factor III — strict separation of config from code; X — dev/prod parity.)*
8. **Sticky sessions masking stateful workloads** — session affinity enabled to paper over in-process per-user state instead of externalising state, defeating horizontal scale and clean failover. *(Twelve-Factor VI — processes are stateless and share-nothing; IX — disposability. Cross-session note — SignalR scale-out via Redis backplane overlaps session 08.)*
9. **Cloud-adapter port leaks provider types** — a port (storage, secrets, messaging, config) exposes `BlobClient`, `SecretClient`, `PubSubClient`, or other provider SDK types in its signature, so the application ring compiles against a specific cloud and is not genuinely swappable. *(Hombergs — does the adapter reflect the port or does infrastructure leak inward? Martin, *Clean Architecture* — the Dependency Rule.)*
10. **Network policy not default-deny** — no `NetworkPolicy` objects, or an allow-all posture, so any pod can reach any other pod and the blast radius is the whole cluster. *(Google Cloud Architecture Framework — Security, defence in depth / micro-segmentation; WAF — Security pillar, network segmentation.)*
11. **GitOps drift via imperative changes** — ArgoCD/Flux claimed but cluster mutated with `kubectl apply`/`helm upgrade` out of band, so the Git repo is no longer the source of truth and reconciliation fights manual edits. *(Twelve-Factor X — dev/prod parity; WAF — Operational Excellence, single declarative source of truth.)*
12. **Helm chart hygiene gaps** — values not parameterised per environment, hard-coded image tags, missing resource requests/limits, no readiness/liveness probes in the template, secrets templated as plain `ConfigMap`. *(Google Cloud Architecture Framework — Operational Excellence, parameterised deployments; WAF — Reliability, health-probe configuration.)*
13. **One-shot job modelled as a long-running service** — migrations or seeders run inside the web container at startup rather than as a discrete run-to-completion `Job` / Cloud Run Job / Container Apps job, coupling schema change to app rollout and racing across replicas. *(Twelve-Factor XII — admin/management tasks as one-off processes; Newman — release/deployment separation.)*
14. **Cloud Run / revision rollout without traffic management** — new revisions take 100% traffic immediately with no weighted split or pinned previous revision for rollback, so there is no progressive-delivery seam at the platform layer. *(Google Cloud Architecture Framework — Operational Excellence, safe progressive rollouts. Note — deployment *strategy* proper is session 09; here judge only the platform-level revision/traffic capability.)*
15. **API gateway responsibilities leaking into services** — authentication, routing, or rate-limiting duplicated inside each service instead of terminated at the gateway, or conversely a gateway absent so every service re-implements edge concerns. *(Hohpe & Woolf — Message Router / edge mediation; Newman — API gateway as the single ingress seam.)*
16. **Service mesh cargo-culted** — Istio/Linkerd installed for a modular monolith or a two-service deployment where the mTLS, traffic-shaping, and observability it provides are not actually exercised, adding operational weight with no justification. *(Newman — adopt mesh only when service count and cross-service policy justify it; WAF — Operational Excellence, avoid unjustified complexity.)*
17. **Service discovery hard-wired per mode** — service endpoints hard-coded or resolved differently in Local vs Azure vs GCP with no single discovery abstraction, so wiring is duplicated and drifts. *(Twelve-Factor IV — treat backing services as attached resources resolved from config; Newman — service discovery.)*
18. **Image provenance unverifiable** — images pushed to the registry without immutable tags or digests, no signing/attestation, `:latest` deployed to environments, so what runs in prod cannot be traced to a build. *(Google Cloud Architecture Framework — Security, supply-chain integrity; WAF — Security, artifact provenance. Cross-link — SBOM/signing depth is session 04.)*

### Cross-Cutting Concerns (config & platform wiring)
1. **Config in code, not environment** — environment-specific settings compiled in or selected by a hard-coded `#if`/hostname switch rather than supplied by the environment per Twelve-Factor. *(Twelve-Factor III — config in the environment.)*
2. **Secrets in source control** — connection strings, keys, or passwords in committed `appsettings.json`/`appsettings.Development.json` rather than user-secrets / Key Vault / Secret Manager. *(Twelve-Factor III. Cross-session note — secrets-management rigour is owned by session 04; flag the presence here, defer the depth.)*
3. **No per-environment override seam** — a single flat config with no `appsettings.{Environment}.json` layering or environment-variable precedence, so per-mode values can't be supplied without editing the base file. *(Twelve-Factor III; Microsoft .NET configuration layering guidance — paraphrase.)*
4. **Centralised config not wired** — App Configuration / Runtime Config / Key Vault referenced in an ADR but the host never registers the provider, so it falls back to local files silently. *(Source-of-truth rule — document claims centralised config, code resolves locally; default High.)*
5. **Feature flags conflated with config** — kill-switches and runtime toggles stored as ordinary static config requiring a redeploy to flip, with no runtime-evaluation seam, defeating the point of a flag. *(Paraphrase — a feature flag that needs a redeploy is just config; flag the conflation.)*
6. **Feature-flag hygiene gaps** — flags with no owner, no default-off for risky paths, no cleanup of stale flags, evaluated inconsistently across services. *(Paraphrase / Microsoft Feature Management guidance; flag the inconsistency.)*
7. **Gateway as a pass-through only** — an API gateway present but performing no auth/rate-limit/routing consolidation, so it adds a hop without earning its place; or edge concerns smeared across services with no gateway. *(Newman — API gateway responsibilities; Hohpe & Woolf — Message Router.)*
8. **Config drift between modes** — the same logical setting sourced from three unrelated mechanisms across Local/Azure/GCP with no single resolution contract, so a value can be right in one mode and wrong in another. *(Twelve-Factor X — dev/prod parity.)*
9. **Service mesh / discovery duplicating platform features** — mesh or bespoke discovery layered on top of a platform (Container Apps, Cloud Run) that already provides ingress, mTLS, and service resolution, duplicating capability. *(WAF — Operational Excellence, avoid redundant infrastructure; Newman — prefer platform primitives.)*

---

## Mini flow-traces for this session (Method 3, scoped)

Trace at the **platform / provisioning altitude**, not for business correctness. Both traces exist to answer one question — *is the tri-mode abstraction real or aspirational?*

- **One cloud-adapter port across all three modes:** pick a single port (storage, secrets, messaging, or config) and follow its interface → Local adapter → Azure adapter → GCP adapter. *Watch:* does the port signature stay provider-neutral, or does a provider-specific type (`BlobClient`, `SecretClient`, a GCP `PubSubClient`) leak through into the application ring? Are all three adapters actually implemented and registered, or is one a stub that throws? Every place a provider concretion crosses the port inward is an `INF` finding (dependency-rule break).
- **Configuration / secret resolution per mode:** pick one connection string or secret and trace where it *actually* comes from in Local (user-secrets? `appsettings.Development.json`?), Azure (Key Vault? App Configuration? env var?), and GCP (Secret Manager? Runtime Config? env var?). *Watch:* is there a single resolution contract, or three unrelated mechanisms? Does any mode fall back to a committed file? Does centralised config that an ADR claims actually get registered in the host? Every discontinuity or silent local fallback is an `INF` finding.

Mark every discontinuity ("this is where tri-mode parity breaks") as an `INF` finding.

---

## Primary anchors for this session

Lead with these (mostly Tier 2, per `00-common-core.md` §8): **Azure Well-Architected Framework** *and* **Google Cloud Architecture Framework** — cite the specific **pillar** (Security, Reliability, Operational Excellence, Cost, Performance) for every cloud-topology verdict, and hold the two frameworks in parallel because the system is tri-mode. **The Twelve-Factor App** — cite the numbered factor by name (III Config, II/IV Dependencies & Backing Services, VI Processes / stateless, VII Port Binding, IX Disposability, X Dev/Prod Parity, XII Admin Processes) for config, statelessness, and parity findings. **Newman, *Building Microservices*** — deployment, environment definition, evolution, gateway, and discovery boundaries (applies to a modular monolith too). For the **cloud-adapter execution check**, lead with **Hombergs** (does the adapter reflect the port, or does provider detail leak inward?) and **Martin, *Clean Architecture*** (the Dependency Rule) — this is the one place Tier 1 leads. Reference implementations to compare the tri-mode adapter and IaC shape against: **eShop** (container/compose + Aspire-style wiring), **MassTransit / Wolverine samples** (transport-swappable adapters as a model for provider-swappable ports), and the **Azure / GCP quickstart IaC** for Container Apps and Cloud Run as the idiomatic-template baseline. Label each big-ticket difference **intentional**, **drift**, or **missing**.

---

## ADR touchpoints to verify (Method 1)

Open every ADR touching: the **tri-mode cloud strategy** (Local/Azure/GCP) and what "swappable" is claimed to mean; **IaC tooling** (Bicep *and* Terraform, and why both); the **cloud-adapter / Ports-&-Adapters** pattern; **container and orchestration** choices (Container Apps vs Cloud Run vs AKS, Docker baseline); **API gateway**; **configuration management** (centralised config, secrets sourcing per mode); and **service mesh** adoption (or the decision *not* to adopt one). For each: *claimed decision | code reality | faithful / drifted / silent / missing | evidence (file:line)*. Pay special attention to **execution fidelity** — a tri-mode ADR is the easiest place in the whole system for the document to claim a clean abstraction that the code has quietly leaked through. Also surface **silent** decisions: an undocumented base-image choice, an out-of-band manual cloud resource, a mesh installed with no ADR, or a config provider registered with no decision record. Record all of these in the partial's **ADR touchpoints** block for session 99 to consolidate.

---

## Output

Write the partial report following the **partial-report skeleton in `00-common-core.md` §11**, covering §12 and §10, with all findings under the `INF` prefix and a scorecard-contribution row for each of the two dimensions (Infrastructure & Cloud; Cross-Cutting Concerns). Carry the **Cross-session notes** forward so session 99 reconciles rather than double-counts: **Redis backplane / SignalR scale-out** overlaps session 08; **health-check readiness/liveness** (Helm probes, orchestrator wiring) overlaps sessions 05 and 07; **secrets management** (Key Vault / Secret Manager depth, SBOM, artifact signing) overlaps session 04. Then stop — the report is the work.
