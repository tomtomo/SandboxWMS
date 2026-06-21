# Phase 02b — Event-Contract Catalog (AsyncAPI) + Tactical DDD Conventions

**Status:** planned

**Pre-conditions:**
- **02a done:** pipeline behavior baseline (`Logging→Authorization→Validation→Transaction→Handler`) + `Result`→transport mapping + FF #7 hijau; slice 01c sudah return `Result`.
- Lanjutan **Phase 02 Harden** (prinsip 2): governance kontrak + emission policy = bagian template, BASELINE; bukan ekspansi event baru.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0023-event-contract-catalog-asyncapi.md` (AsyncAPI 3.0 in-source, logical identity `{module}.{event_snake}.v{N}`, SemVer bump rule, contract-coverage FF parse YAML + reflect `*.Contracts`, reuse harness BUKAN NetArchTest murni, directional only)
- `docs/adr/0026-tactical-ddd-conventions.md` (identity-encoding, emission policy — event dari dalam aggregate hanya pada fakta sukses, VO ownership, behavioral fitness category)
- `docs/adr/0005-event-driven-outbox.md` (logical event identity + SemVer Amendment; saga boundary RULE; composite inbox key)
- `docs/adr/0009-contracts-vs-grpc-separation.md` (`*.Contracts` POCO zero transport dep)
- `docs/adr/0010-data-ownership-db-per-service.md` (Amendment: `IDeadLetterStore`, Local tabel `dead_letter`; retry/DLQ)

**Tujuan:** Bikin satu artefak otoritatif seam EDA (`asyncapi.yaml`) + fitness function yang menjaganya sinkron dengan tipe `*.Contracts`, kodifikasi emission policy taktis lewat behavioral test, dan pasang retry→DLQ baseline di consumer Inventory.

**Deliverable:**
- `docs/architecture/asyncapi.yaml` (**AsyncAPI 3.0**) — channel `inbound.gr_confirmed.v1` (terisi penuh) + **placeholder** channel `outbound.wave_released.v1` / `inventory.stock_allocated.v1` / `outbound.shipment_dispatched.v1` / `outbound.picking_completed.v1`; sertakan matriks **emitter→receiver→trigger** + kolom **emitted-but-unconsumed** (tandai `StockLow`/`StockNearExpiry` sbg gap eksplisit).
- **Logical-name binding** pada tipe `*.Contracts` (attribute/const statik), mis. `GRConfirmedV1` → `inbound.gr_confirmed.v1`.
- `tests/Wms.Architecture.Tests`: **FF #11 contract-coverage** — parse `asyncapi.yaml` + reflect tipe `*.Contracts`; tiap tipe published wajib punya channel terdeklarasi (directional). **Reuse harness** Architecture.Tests, BUKAN NetArchTest murni (baca artefak YAML eksternal).
- **Behavioral aggregate-emission test**: method `GoodsReceipt.Confirm()` me-raise `GoodsReceiptConfirmed` domain event (kategori behavioral ADR-0026, di test suite — bukan FF statik).
- **Retry + DLQ baseline** ter-wire di consumer Inventory (`GRConfirmedV1` handler) lewat `IDeadLetterStore` yang sudah ada (Local: tabel `dead_letter`).

**Tasks:**
1. Tulis `docs/architecture/asyncapi.yaml` (AsyncAPI 3.0): channel `inbound.gr_confirmed.v1` lengkap (payload `grId, warehouseId, receivedLines[]`) + 4 placeholder channel di atas; matriks emitter→receiver→trigger + kolom emitted-but-unconsumed.
2. Pasang logical-name binding di `Wms.Inbound.Contracts.GRConfirmedV1` (attribute/const `inbound.gr_confirmed.v1`) sebagai pola yang diikuti contract berikutnya.
3. Implement **FF #11**: parser `asyncapi.yaml` + reflection `*.Contracts` → assert tiap tipe published punya channel; reuse harness `Wms.Architecture.Tests`.
4. Behavioral test aggregate-emission: panggil `GoodsReceipt.Confirm()` → assert `GoodsReceiptConfirmed` ter-raise (dan tak ter-raise pada guard gagal — emission policy ADR-0026).
5. Wire retry→DLQ baseline di Inventory consumer: gagal handle berulang → poison message ditulis ke `IDeadLetterStore`; gunakan composite inbox key `(event_id, handler_type)` yang sudah ada.
6. Catat `outbound.picking_completed.v1` (`PickingCompletedV1`) sebagai placeholder channel — event ke-5 yang **diputuskan di ADR-0028** (`docs/adr/0028-picking-completed-event.md`); jangan implement consumer di sini (itu Phase 03b/03c).

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **FF #11 pass** (`GRConfirmedV1` punya channel `inbound.gr_confirmed.v1`).
- Behavioral aggregate-emission test hijau (`Confirm()` raise `GoodsReceiptConfirmed`).
- Integration test consumer retry→DLQ hijau (poison `GRConfirmedV1` mendarat di `dead_letter` setelah retry habis).

**Learning objective:** AsyncAPI 3.0 catalog sebagai kontrak otoritatif; contract-coverage fitness function (membaca artefak eksternal, di luar NetArchTest); logical event identity + SemVer bump rule; tactical DDD emission policy (event dari aggregate, hanya fakta sukses); Dead Letter Channel.

**Out-of-scope:** AsyncAPI CLI validate sebagai CI gate (ditolak ADR-0023, narik toolchain Node); reverse-coverage (tiap receiver punya emitter) = known gap; wave cancel / allocation failure (global out-of-scope) → biarkan event unconsumed tak ber-consumer.

**Handoff notes:** Seam EDA kini otoritatif (`asyncapi.yaml` Kelas A) + dijaga FF #11; emission policy diverifikasi behavioral test; consumer Inventory punya retry/DLQ baseline. **02c** menutup template: SYSTEM actor + audit out-of-band + correlation-id + OTel baseline. Channel placeholder diisi penuh saat event-nya lahir di Phase 03.

**Touchpoint cert:** **No cert touchpoint** (architecture governance — AsyncAPI, fitness function, tactical DDD; bukan objektif AZ-204 maupun PCD).

> Note: `asyncapi.yaml` adalah artefak **Kelas A** otoritatif (`docs/architecture/`), legitim dibuat di phase ini.
