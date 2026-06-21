# Phase 01a — Solution Skeleton + BuildingBlocks + 6 Fitness Functions

**Status:** done (2026-06-21)

**Pre-conditions:**
- Belum ada `src/`. Terpasang: .NET 8 SDK (+ 9.x/10.x), Docker Desktop. **Aspire via NuGet** (`Aspire.AppHost.Sdk`, Aspire ≥9 — workload tak diperlukan); SDK dipin ke 9.x via `global.json`; library target `net8.0` (LTS baseline ADR-0007).
- Bagian dari **Phase 01 Walking Skeleton** — deliverable E2E penuh tercapai di akhir **01c**; 01a menyiapkan fondasi + harness (bukan milestone infra-only berdiri sendiri).

**Context refs (WAJIB baca dulu):**
- `docs/architecture/tri-cloud-microservices-blueprint.md` (§3 solution tree, §4 dependency rule + 6 fitness function)
- `docs/adr/0003-clean-architecture-dependency-rule-fitness-functions.md`
- `docs/adr/0002-tri-cloud-hexagonal.md` · `docs/adr/0008-aspire-distributed-local.md` · `docs/adr/0007-monorepo-with-polyrepo-path.md` (CPM)

**Tujuan:** Berdirikan solution monorepo `Wms` — BuildingBlocks seedwork + project skeleton + 6 fitness function (NetArchTest) hijau + Aspire AppHost — sebagai guardrail terkunci sebelum kode bisnis ditulis.

**Deliverable:**
- `Wms.sln` + `Wms.Local.slnf` · `Directory.Build.props` · `Directory.Packages.props` (CPM) · `.editorconfig` · `nuget.config`.
- `src/BuildingBlocks/Wms.BuildingBlocks.{Domain,Application,Infrastructure,Web}` — seedwork: `AggregateRoot`, `Entity`, `ValueObject`, `StronglyTypedId<T>`, `IDomainEvent`, `Result`/`Result<T>`/`Error` (`Error.Type` ∈ Validation/NotFound/Conflict/Unauthorized/Unexpected), `ICommand`/`IQuery`, `IEndpoint`.
- `src/Platform/Wms.Platform.Hosting` (`AddServiceDefaults`: logging+health, NOL cloud SDK) + `Wms.Platform.Local`.
- Placeholder module `src/Modules/Inbound/Wms.Inbound.{Domain,Application,Infrastructure,Api,Contracts}` + `src/Hosts/Local/Wms.Inbound.Host.Local`.
- `src/AppHost/Wms.AppHost` (Aspire) — declare Postgres + Inbound host.
- `tests/Wms.Architecture.Tests` — FF #1–#6.

**Tasks:**
1. `dotnet new sln -n Wms`; tambah `Directory.Build.props` (.NET 8, nullable, implicit usings), `Directory.Packages.props` (pin NetArchTest, xUnit, Aspire, EF Core), `.editorconfig`, `nuget.config`.
2. `Wms.BuildingBlocks.Domain` — seedwork lengkap di atas (`Result`/`Error` jadi fondasi ADR-0019 nanti).
3. `Wms.BuildingBlocks.{Application,Infrastructure,Web}` dgn arah referensi persis blueprint §4 (Application→Domain; Infrastructure→Application; Web→Application).
4. `Wms.Platform.Hosting` (`AddServiceDefaults`) + `Wms.Platform.Local` (placeholder adapter project).
5. Placeholder module Inbound (5 project kosong, cukup jadi target FF) + `Wms.Inbound.Host.Local`.
6. `Wms.AppHost` (Aspire 9 NuGet — `<Sdk Name="Aspire.AppHost.Sdk">` + `Aspire.Hosting.AppHost`/`Aspire.Hosting.PostgreSQL`, no workload) — declare resource Postgres + reference Inbound host.
7. `tests/Wms.Architecture.Tests` (NetArchTest+xUnit) — implement FF #1–#6 persis blueprint §4 / ADR-0003.
8. Masukin semua project ke `Wms.sln` + `Wms.Local.slnf`.

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **6 fitness function pass**.
- `dotnet run --project src/AppHost/Wms.AppHost` → Aspire dashboard naik, resource Postgres healthy.

**Learning objective:** Clean Architecture Dependency Rule sebagai *executable* fitness function (NetArchTest, architecture-as-code); Hexagonal port placement; monorepo + Central Package Management; Aspire distributed-local bootstrap.

**Handoff notes:** Solution `Wms` + 6 FF hijau = guardrail semua phase. Seedwork (`Result`, `AggregateRoot`, `StronglyTypedId`, `IDomainEvent`) siap. **01b** menambah Outbox/Inbox di `BuildingBlocks.Infrastructure` + `IMessagePublisher` Local adapter di atas fondasi ini.

**Touchpoint cert:** AZ-204 — *no cert touchpoint langsung* (fondasi struktural; Aspire→ACA baru di Phase 05). PCD — *no cert touchpoint langsung* (Aspire→Cloud Run baru di Phase 06).
