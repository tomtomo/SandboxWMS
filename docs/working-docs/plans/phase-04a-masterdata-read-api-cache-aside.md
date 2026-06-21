# Phase 04a — MasterData Read-API + Cache-Aside + Resilience

**Status:** planned

**Pre-conditions:**
- **03c done:** core flow E2E (Inbound→Inventory→Outbound s/d `ShipmentDispatched`) hijau; `GoodsReceipt.expectedLines[]` + `OutboundOrder.orderLines[]` masih pakai seed lokal (uom/batchTracking) — MasterData belum ada.
- Building-block templates Phase 02 (Result→transport, MediatR pipeline, DeadLetter, audit) terpasang & reusable. Awal **Phase 04 supporting** (prinsip 3, setelah core).

**Context refs (WAJIB):**
- `docs/adr/0011-master-data-read-api-cache-aside.md` (gRPC read-API + cache-aside + `ICacheStore` + targeted bypass) · `docs/adr/0014-snapshot-vs-reference-master-data.md` (snapshot kritikal + soft-delete)
- `docs/adr/0006-grpc-internal-rest-ui.md` (gRPC internal) · `docs/adr/0020-resilience-pipeline-defaults.md` (Polly v8 split-timeout — first cross-service gRPC consumer) · `docs/adr/0021-service-to-service-auth.md` (`IServiceTokenProvider` + interceptor)
- `docs/tomsandboxwms-overview.md` §D

**Tujuan:** Berdirikan MasterData (supporting, collapsed) sebagai authority Warehouse/Location/Product; expose gRPC read-API; pasang cache-aside + Polly resilience; ganti seed 01c/03 dengan snapshot dari MasterData via gRPC.

**Deliverable:**
- `Wms.MasterData.{Domain,Application,Infrastructure,Api,Contracts,Grpc}` (collapsed right-sizing). Aggregate `Warehouse`/`Location`/`Product` (lifecycle sederhana `isActive` soft-delete). CRUD slices (`Features/<UseCase>`).
- `Wms.MasterData.Grpc`: `.proto` `GetProduct`/`GetWarehouse`/`GetLocation`. `Wms.MasterData.Api` implement gRPC service via **reader-delegation read-port** (NOT `DbContext` → FF#8) + expose REST CRUD.
- `ICacheStore` port (get/set/remove + TTL) + `InMemoryCacheStore` di `Wms.Platform.Local`; read path **cache-aside TTL-first** (miss→populate, hit→served).
- `ResiliencePipelineDefaults` factory (Polly v8) di `Wms.BuildingBlocks.Infrastructure`: Timeout→Retry→CircuitBreaker, **split timeout gRPC ~30s / HTTP ~5s**; applied ke gRPC client.
- `IServiceTokenProvider` Local trust-stub + gRPC client interceptor (sisipkan bearer).
- Wire Inbound (`expectedLines` snapshot `uom`+`batchTracking`) + Outbound (`orderLines`) memanggil MasterData read-API → replace seed 01c/03.
- Global soft-delete query filter (`isActive`) + **filter-name-targeted bypass** (NOT blanket `IgnoreQueryFilters`).
- FF#8 (gRPC `*.Api` tak sentuh `DbContext`) di `tests/Wms.Architecture.Tests`.

**Tasks:**
1. Aggregate `Warehouse`/`Location`/`Product` + CRUD vertical slices; `Wms.MasterData.Infrastructure` DbContext schema `masterdata` + global query filter `isActive`.
2. `.proto` (`GetProduct`/`GetWarehouse`/`GetLocation`) di `Wms.MasterData.Grpc`; service impl di `Wms.MasterData.Api` via read-port reader-delegation (jangan inject `DbContext`).
3. `ICacheStore` port (BuildingBlocks.Application) + `InMemoryCacheStore` (Platform.Local); bungkus read path cache-aside TTL-first.
4. `ResiliencePipelineDefaults` factory (Polly v8) di BuildingBlocks.Infrastructure (split-timeout); register gRPC client (`ResiliencePipelineProvider<string>` per-client key).
5. `IServiceTokenProvider` Local stub + gRPC client interceptor untuk bearer + correlation.
6. Targeted soft-delete bypass (filter-name) di read-port untuk path yang harus lihat inactive.
7. Wire Inbound `expectedLines` snapshot uom/batchTracking + Outbound `orderLines` ke MasterData read-API; hapus seed.
8. `Wms.MasterData.Host.Local` declare di `Wms.AppHost`. FF#8 + behavioral split-timeout test.

**Definition of Done:**
- `dotnet build Wms.sln` hijau; **FF#8 baru + behavioral `split-timeout-configured` + semua FF hijau**.
- Integration: Inbound `CreateGoodsReceipt` men-snapshot `uom` dari MasterData via gRPC (cache **miss→populate**, **hit→served from cache**); soft-delete menyembunyikan Product inactive, targeted bypass tetap melihatnya.

**Out-of-scope:** Event-driven cache invalidation (`ProductUpdated`) — TTL-first only (ADR-0011 dicatat-tak-diaktifkan). Redis/Memorystore adapter (branded Phase 05/06). `Location` nested hierarchy.

**Learning objective:** gRPC read-API (protocol fit-for-purpose), cache-aside (lazy load + TTL), DB-per-service boundary via kontrak (bukan shared schema), snapshot vs reference, Polly resilience split-timeout (cold-start absorb), reader-delegation (gRPC `.Api` bebas `DbContext`).

**Handoff notes:** MasterData hidup; core flow konsumsi master via gRPC read-API + cache-aside + Polly. `ICacheStore`/`IServiceTokenProvider`/`ResiliencePipelineDefaults` siap dipakai 04b/04d. **04b** menyusul Auth (authN identity → ganti SYSTEM-actor) di atas read-API pattern ini.

**Touchpoint cert:** AZ-204 — Azure Cache for Redis (cache-aside pattern; branded di 05) + resilient apps/Polly. PCD — Memorystore (cache-aside pattern; branded di 06).
