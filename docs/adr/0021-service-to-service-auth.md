# ADR-0021: Service-to-service authentication pada seam gRPC internal

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** Port `IServiceTokenProvider` (BuildingBlocks), adapter `Platform.<Cloud>`, gRPC client/server interceptor; seam internal [ADR-0006](0006-grpc-internal-rest-ui.md)

## Context

Komunikasi gRPC antar-service ([ADR-0006](0006-grpc-internal-rest-ui.md)) saat ini **implicitly unauthenticated** — tak ada keputusan tentang bagaimana service pemanggil membuktikan identitasnya ke callee, padahal hop ini melintasi proses (dan lintas-cloud). Kebutuhan: caller punya identitas yang bisa diverifikasi, audience-scoped per callee, tanpa menyeret cloud SDK ke core.

## Decision

- **Pilihan:** **OAuth2 bearer audience-scoped** per callee, diperoleh di balik **port core-neutral `IServiceTokenProvider`** dan disisipkan oleh **gRPC client interceptor**. Adapter compile-time bound: Azure **Managed Identity**, GCP **Service Account OIDC ID-token**, Local **trust stub** (token kosong). Sisi server: callee `[Authorize]` audience policy, `Authority`=issuer cloud, `ValidateAudience=true`. Validasi token **offline** (verify signature lokal), bukan call sinkron ke Auth tiap request.
- **Kenapa:** Mengisi celah hop internal yang tak terautentikasi dengan shape port/adapter yang sama seperti port lain ([ADR-0002](0002-tri-cloud-hexagonal.md)) — gabung ke `IMessagePublisher`/`ISecretProvider`. Platform-issued identity (MI/SA) menghindari secret bersama. `→ Canon: Newman (Building Microservices), service-to-service security & token propagation; OWASP ASVS, service authentication; MS Learn: Managed Identity; Google Cloud: Service Account OIDC`.
- **Trade-off:** Tiap host harus tahu issuer/audience-nya; menambah interceptor di kedua sisi; token offline-validation berarti revocation bergantung lifetime token (sama seperti JWT user).
- **Kapan ditinjau ulang:** Bila butuh mutual-TLS / SPIFFE-style workload identity, atau zero-trust mesh → eskalasi terdokumentasi.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. Bearer audience-scoped via port + MI/SA adapter, offline-validate** *(dipilih)* | Tanpa secret bersama; idiomatik tiap cloud; offline (hot-path bersih) | Interceptor 2 sisi; revocation by lifetime | Newman (Building Microservices); MS Learn (MI) |
| B. Shared static key / HS256 antar-service | Paling sederhana | Secret bersama ke semua host; alg-confusion; musuh isolation | OWASP ASVS |
| C. mTLS / SPIFFE workload identity | Kuat, mutual | Ops berat untuk solo; berlebih sekarang | Newman (Building Microservices) |

## Consequences

**Positif**
- Hop gRPC internal jadi terautentikasi; identity caller dapat diverifikasi & di-audit ([ADR-0022](0022-operational-audit-log.md)).
- Payload cert kuat: AZ-204 Managed Identity, GCP SA OIDC.
- **Fitness function**: tak ada `Local*TokenProvider` di-reference oleh cloud host ([ADR-0003](0003-clean-architecture-dependency-rule-fitness-functions.md)).

**Trade-off / lebih sulit**
- Background/non-HTTP origin (scan quarantine-aging, low-stock) memakai **SYSTEM actor** ([ADR-0027](0027-system-actor-convention.md)) sebagai principal, bukan user.

**Yang harus dijaga**
- **Dua bidang token dibedakan tegas:** (a) **s2s token** = di-mint & ditandatangani **platform cloud** (Entra ID Managed Identity / Google SA OIDC), divalidasi terhadap **issuer/Authority cloud** — *bukan* ditandatangani auth-svc; (b) **user JWT** = di-mint auth-svc dengan RS256 ([ADR-0016](0016-refresh-token-rotation.md)). Keduanya **verify offline** (tak ada RPC ke Auth per-request); validasi via public-key yang didistribusi offline. (JWKS endpoint/`kid` rotation tetap *deferred* per [ADR-0016](0016-refresh-token-rotation.md).)

## Out of scope / deferred

- External IdP / `IIdentityProvider` / OIDC token-exchange / auto-provisioning → tetap deferred (surface besar; jangan di-un-defer di sini).
- mTLS/SPIFFE & edge pre-validation (APIM) → future escalation.
