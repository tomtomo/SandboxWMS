# Phase 07c — Resilience Calibration + Durable Delayed Tasks

**Status:** planned

**Pre-conditions:**
- **07a done:** authZ aktif lintas sistem (dua cloud). `ResiliencePipelineDefaults` factory (Polly v8) dari `BuildingBlocks.Infrastructure` ada (split-timeout di-set tapi belum dikalibrasi traffic nyata); `IDelayedTaskQueue`/`IDelayedTaskHandler` masih adapter Local in-memory (ADR-0025); `StockQuarantineStale` masih emitted-but-unconsumed.
- Bagian **Phase 07 Cross-Cutting Wide (FINAL)** — DEEP pass resilience di atas baseline.

**Context refs (WAJIB baca dulu):**
- `docs/adr/0020-resilience-pipeline-defaults.md` (Timeout→Retry→CircuitBreaker; split timeout gRPC ~30s vs HTTP ~5s; FF behavioral `split-timeout-configured`; angka provisional sampai traffic nyata)
- `docs/adr/0025-cross-cutting-platform-ports.md` (`IDelayedTaskQueue` durable — Service Bus scheduled `ScheduledEnqueueTimeUtc` / Cloud Tasks + OIDC; anchor quarantine-aging → `StockQuarantineStale`)

**Tujuan:** Kalibrasi Polly di bawah traffic cloud nyata (validasi split-timeout vs cold-start scale-to-zero); jadikan `IDelayedTaskQueue` durable (survive restart) menggantikan Local in-memory; tutup satu event emitted-but-unconsumed via quarantine-aging → `StockQuarantineStale` → email.

**Deliverable:**
- Kalibrasi Polly di traffic nyata: validasi gRPC ~30s menyerap cold-start (scale-to-zero) sementara HTTP ~5s fail-fast; tune retry count / circuit-breaker ratio (update tabel knob ADR-0020 bila berubah = ubah ADR).
- Behavioral FF **`split-timeout-configured`** (timeout gRPC ≠ HTTP & keduanya ter-set) — inspeksi nilai config runtime, di registry ADR-0003.
- `IDelayedTaskQueue` + `IDelayedTaskHandler` adapter **durable**: Azure Service Bus scheduled messages (`ScheduledEnqueueTimeUtc`) + GCP Cloud Tasks (+OIDC) — menggantikan Local in-memory.
- Quarantine-aging scan → emit **`StockQuarantineStale`** (origin **SYSTEM actor**, ADR-0027) → consumer Notification → **email** (overview §G) — menutup satu emitted-but-unconsumed event (ADR-0023).

**Tasks:**
1. Jalankan beban realistis ke service scale-to-zero; ukur cold-start gRPC; konfirmasi 30s menyerap, 5s seragam akan gagal di attempt-1.
2. Tune retry/circuit-breaker berdasar observasi (trace dari 07b); update knob + ADR-0020 bila angka berubah.
3. Implement/verifikasi behavioral FF `split-timeout-configured` (assert timeout gRPC ≠ HTTP, keduanya ter-set).
4. `ServiceBusDelayedTaskQueue` (Azure, `ScheduledEnqueueTimeUtc`) — adapter `IDelayedTaskQueue` durable.
5. `CloudTasksDelayedTaskQueue` (GCP, Cloud Tasks + OIDC) — adapter durable; wire `IDelayedTaskHandler` dispatch.
6. Quarantine-aging scan men-schedule via `IDelayedTaskQueue` → handler emit `StockQuarantineStale` dengan principal SYSTEM (ADR-0027).
7. Daftarkan `StockQuarantineStale` channel di `asyncapi.yaml`; wire consumer Notification → kirim email (idempotent, ADR-0017).
8. Test: durable delayed task fires setelah delay + **survive host restart**; quarantine-aging→`StockQuarantineStale`→email delivered (integration).

**Definition of Done:**
- `dotnet build Wms.sln` hijau.
- `dotnet test tests/Wms.Architecture.Tests` hijau — **behavioral FF `split-timeout-configured`** hijau.
- Call gRPC cold-start **survive (~30s)** di mana HTTP timeout seragam (~5s) akan gagal (smoke cloud).
- Durable delayed task **fires setelah delay AND survive host restart** (behavioral); contract-coverage FF#11 hijau dengan `StockQuarantineStale` ber-channel.
- Quarantine-aging → `StockQuarantineStale` → **email terkirim** (integration).

**Learning objective:** Polly v8 resilience (rationale split-timeout vs scale-to-zero cold-start); tuning circuit-breaker/retry; durable delayed-task scheduling (Service Bus scheduled / Cloud Tasks); reliability-degree taxonomy (ADR-0025).

**Handoff notes:** Polly terkalibrasi traffic nyata; `IDelayedTaskQueue` durable di dua cloud (survive restart); satu emitted-but-unconsumed event (`StockQuarantineStale`) ditutup end-to-end ke email. **07d** mengeraskan jalur kredensial/secret yang dipakai adapter durable ini.

**Out-of-scope:** WaveSaga allocation-timeout (saga deferred, ADR-0005/ADR-0025); `StockLow`/`StockNearExpiry` tetap emitted-but-unconsumed (JANGAN bangun consumer-nya).

**Touchpoint cert:** AZ-204 — Service Bus scheduled messages, resilient apps (Polly), scale-to-zero cold-start → X. PCD — Cloud Tasks (+OIDC), resilient services, Cloud Run cold-start → X.
