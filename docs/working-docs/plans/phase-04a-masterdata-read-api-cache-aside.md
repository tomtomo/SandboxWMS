# Phase 04a — MasterData Read-API + Cache-Aside + Resilience

**Status:** done (2026-06-22)

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

**Handoff notes (done 2026-06-22):**
- **Dibangun:** modul `Wms.MasterData` FULL-6 (Domain/Application/Infrastructure/Api/Contracts/Grpc) + `Host.Local`. Aggregate Warehouse/Location/Product (TDD RED→GREEN, soft-delete `isActive` lifecycle, 27 domain test). gRPC read-API `GetProduct/GetWarehouse/GetLocation` (by-id) via **reader-delegation** (`IMasterDataReader` → `MasterDataReader` EF; `.Api` bebas DbContext → FF#8). NotFound dipetakan `ResultExceptionInterceptor` (server) → RpcException. REST CRUD = **Create + Deactivate** per aggregate (Update di-defer). `Wms.MasterData.Contracts` = placeholder (read-only authority, nol event; reserve slot `ProductUpdated` ADR-0011 amendment).
- **Building-block BARU:** `ICacheStore` + `IServiceTokenProvider` port (BuildingBlocks.Application); `ResiliencePipelineDefaults` factory Polly v8 (Timeout→Retry→CircuitBreaker, **split-timeout gRPC 30s/HTTP 5s**, key `wms-grpc`) + behavioral FF `split-timeout-configured`; `ServiceAuthCallCredentials` gRPC client (bearer+correlation, async-native `CallCredentials.FromInterceptor`) di BuildingBlocks.Web. `InMemoryCacheStore` + `LocalServiceTokenProvider` (Platform.Local, `AddLocalCaching`/`AddLocalServiceTokenProvider`). **ServiceDefaults diubah:** HTTP `AttemptTimeout`→5s dari single-source factory (realisasi split-timeout HTTP-side; 07c kalibrasi).
- **Cache-aside:** `CachedMasterDataReader` decorator (TTL-first 5min, populate-on-miss, hit-served; invalidasi event `ProductUpdated` DICATAT-TAK-AKTIF ADR-0011). **Soft-delete:** EF Core 8 **tak punya named query filters** (fitur EF 10) → pola **FLAG-GATED single-filter** (`p => IncludeInactive || p.IsActive`) — bypass me-relaks HANYA `isActive` (bukan blanket `IgnoreQueryFilters`), filter lain (warehouse-scoping kelak) tetap → targeting setara per ADR-0014. ⚠ flag.
- **Seed replacement (uom-only):** `IProductCatalog` ACL port + `GrpcProductCatalog` adapter (resilience pipeline + RpcException→null) per modul; Inbound `CreateGoodsReceipt` snapshot uom expectedLines, Outbound `ReceiveOutboundOrder` snapshot uom orderLines; **`OutboundSeed.cs` DIHAPUS**. Host Inbound/Outbound: `AddGrpcClient`+credentials+resilience+`AddMasterDataProductCatalog`. AppHost: `masterdatadb` + host `masterdata`; inbound/outbound `WithReference(masterdata)`. MigrationRunner +`MasterDataDbContext`.
- **DoD terverifikasi:** `dotnet build Wms.sln` 0/0; Architecture **10** (FF#1–8,#11 + behavioral split-timeout); MasterData.Domain 27; MasterData.Integration **4** (migration apply Postgres riil · cache miss→populate/hit→served · soft-delete hide + targeted bypass · **gRPC transport REAL via WebApplicationFactory** → Inbound snapshot uom + unknown→UnknownProduct). **NOL regresi** (160 existing hijau; integration test existing pakai stub `IProductCatalog` di TestSupport).
- **Utang sadar / gap (flag):**
  1. **batchTracking snapshot DI-DEFER** — deliverable sebut "uom+batchTracking", DoD hanya verifikasi uom; batchTracking ke ExpectedLine = dead-data (belum ada enforcement batch-required saat scan) + hindari migrasi Inbound & churn 13 domain test. Aktifkan saat scan-batch-enforcement lahir.
  2. **InventoryLocations DI-DEFER** (keputusan scope Tom) — butuh `GetLocation` by-(warehouse,type), di luar read-API by-id 04a. Follow-up task spawned (`task_2976c27d`).
  3. **gRPC client double-resilience** — pipeline eksplisit 30s + HTTP standard-handler 5s sama-sama kena HttpClient gRPC; opt-out HTTP-default untuk gRPC = 07c (cold-start tak ter-exercise di Local/test).
  4. **Aspire live cross-process gRPC = MANUAL** (belum Claude-run); E2E otoritatif via integration test 1-proses (WebApplicationFactory). `// TODO-AUTH` di REST CRUD slice (MasterData.Manage{Product,Location,Warehouse}) → 07a.
- **CPM +:** Grpc.AspNetCore/Net.ClientFactory/Net.Client/Tools 2.80.0, Google.Protobuf 3.35.1, Polly.Core/Extensions 8.7.0, Microsoft.AspNetCore.Mvc.Testing 8.0.15.
- **Next:** **04b** (auth-jwt-refresh-token-rotation) — authN identity ganti SYSTEM-actor, di atas read-API + cache-aside pattern ini; ATAU 04c (reporting-projections, depends-on 03c). `ICacheStore`/`IServiceTokenProvider`/`ResiliencePipelineDefaults` siap reuse. **Git: UNCOMMITTED — Tom commit (Rule 2).**

**Touchpoint cert:** AZ-204 — Azure Cache for Redis (cache-aside pattern; branded di 05) + resilient apps/Polly. PCD — Memorystore (cache-aside pattern; branded di 06).
