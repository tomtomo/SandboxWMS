# Phase 04d — Notification: Async Delivery + Idempotency + Retry/DLQ

**Status:** done (2026-06-22)

**Pre-conditions:**
- **04b done:** Auth read-API (User/Role) hidup → recipient detail dapat di-resolve via gRPC.
- 04a MasterData read-API + `ResiliencePipelineDefaults` terpasang (warehouse scope). Rel Outbox/Inbox + `IDeadLetterStore` (Phase 02b) reusable.

**Context refs (WAJIB):**
- `docs/adr/0017-eventual-consistency-reporting-notification.md` (pure consumer + async worker + idempotency + retry/DLQ + channel abstraction)
- `docs/adr/0011-master-data-read-api-cache-aside.md` (read Auth/MasterData via gRPC read-API + cache-aside) · `docs/tomsandboxwms-overview.md` §G

**Tujuan:** Berdirikan Notification (pure consumer) — konsumsi domain event → resolve subscription → enqueue `NotificationDelivery`; worker async dispatch ke channel dengan idempotency + retry→DLQ; recipient/warehouse via read-API.

**Deliverable:**
- `Wms.Notification` module (pure consumer, collapsed). Aggregate `NotificationSubscription` + `NotificationDelivery` (Pending/Sent/Failed/Read), schema `notification`.
- Event handlers → resolve `NotificationSubscription` (per user/role + `warehouseScope`) → enqueue `NotificationDelivery`(Pending).
- Worker async **BackgroundService** dispatch → channel; **idempotent** (cek `Sent` sebelum kirim); **retry→DLQ** (reuse `IDeadLetterStore`) setelah max retry.
- Channel ports `IEmailSender` / `IPushNotifier` / `IInAppNotifier` + adapter Local (log / in-memory).
- Konsumsi Auth read-API (recipient detail) + MasterData read-API (warehouse scope) via gRPC (Polly + `IServiceTokenProvider` interceptor dari 04a).
- Default triggers per overview §G: `GoodsReceipt InProgress→Pending`→SPV; `PutawayTask Assigned`→operator; `PickingTask Assigned`→operator; `Wave Active→Ready`→SPV; discrepancy `OverDelivery`→purchasing+SPV.

**Tasks:**
1. Aggregate `NotificationSubscription` + `NotificationDelivery` (state machine Pending/Sent/Failed/Read); DbContext schema `notification`.
2. Channel ports `IEmailSender`/`IPushNotifier`/`IInAppNotifier` + adapter Local (log/in-memory) di `Wms.Platform.Local`.
3. Event handlers (GR→Pending, PutawayTask Assigned, PickingTask Assigned, Wave→Ready, OverDelivery) → resolve subscription → enqueue `NotificationDelivery`.
4. Resolve recipient via Auth read-API + warehouse scope via MasterData read-API (gRPC, Polly + token interceptor).
5. Worker `BackgroundService` dispatch: cek `Sent` (idempotent) sebelum kirim; sukses→`Sent`, gagal→`Failed`+retryCount.
6. Retry policy → `IDeadLetterStore` setelah max retry; in-app mark-as-read (`Read`, hanya InApp).
7. `Wms.Notification.Host.Local` declare di `Wms.AppHost`.
8. Integration tests: enqueue→send (no re-send), failed→retry→DLQ, mark-as-read.

**Definition of Done:**
- `dotnet build Wms.sln` hijau; **semua FF hijau**.
- Integration: event → `NotificationDelivery` enqueued → worker mengirim (duplicate **tidak** re-sent); failed send → retry → **DLQ setelah max retry**; in-app delivery **mark-as-read**.

**Out-of-scope:** SMTP/SendGrid/FCM/APNs konkret (Local adapter log/in-memory only; branded later). Read-tracking Email/Push (hanya InApp `Read`). `Stock Quarantine` aging trigger (di-wire 07c via `IDelayedTaskQueue`). authZ enforcement (Phase 07a).

**Learning objective:** Event consumer (notification handler vs projection handler — beda efek), async worker decoupling (BackgroundService, jangan block flow utama), idempotent delivery (cek Sent), retry/DLQ (Dead Letter Channel, isolasi kegagalan channel provider), channel-provider abstraction (swap adapter).

**Handoff notes:** Notification hidup (collapsed pure-consumer, schema `notification`): `NotificationSubscription` + `NotificationDelivery` (Pending/Sent/Failed/Read, plain AggregateRoot — audit-skip sadar spt Reporting) + `NotificationEnqueuer` (subscription resolve) + 2 notifier Inbox-committed + `NotificationDispatcher` (BackgroundService + `ProcessOnceAsync` testable) + channel ports (`IEmailSender`/`IPushNotifier`/`IInAppNotifier` di BuildingBlocks.Application, adapter log di Platform.Local) + directory ports gRPC (`IUserDirectory`→Auth, `IWarehouseDirectory`→MasterData, Polly+token, wired host) + REST (subscription create / in-app inbox / mark-as-read). Host + AppHost (`notificationdb`, ref auth+masterdata) + MigrationRunner + migration `InitialNotification`. Tests 5/5; full suite hijau; FF 15/15. in-app delivery dipakai WebUI di 04e. Pola pure-consumer + worker sets up Azure Functions (05d) / Cloud Run + Pub/Sub push (06d).

**Keputusan arsitektur — mechanism-first (pilihan Tom, pre-condition gap pola 04c/ADR-0030):** trigger §G (GR→**Pending**, Putaway/Picking→**Assigned**, Wave→**Ready**) tak punya event di katalog (cuma versi *Completed/Confirmed*); Auth read-API tak punya `ListUsersByRole`. Diputuskan **consume event yang ADA, NOL sentuh modul `done`**, fidelity §G dicatat deferred:
- Consume `inbound.gr_confirmed.v1` → SPV (subscription, warehouse-scoped) + **OverDelivery→purchasing AKURAT** (rejectedLines reason=`RejectExcess` = excess over-delivery §A4).
- Consume `outbound.picking_completed.v1` → operator **DIRECT** (recipient = `OperatorId` payload, bukan subscription).
- Subscription model menampung User+Role+warehouseScope (spec §G utuh); resolver merealisasikan **User-direct**; **Role fan-out di-defer** (butuh Auth `ListUsersByRole`).

**Deferred-gap (auditable, calon enrich/ADR — JANGAN dianggap selesai):**
1. Momen §G **Pending/Assigned/Ready** butuh event producer baru (`gr_pending`/`putaway_assigned`/`picking_assigned`/`wave_ready`) — Option A 04c-style bila kelak diminta (sentuh Inbound/Inventory/Outbound + asyncapi + FF#11).
2. **Role→users fan-out** butuh enrich Auth read-API (`ListUsersByRole(roleCode, warehouseId?)`) — subscription Role kini tercatat tapi resolver kembalikan kosong sadar.
3. Channel branded (SMTP/SendGrid/FCM/APNs) + Stock-Quarantine-aging trigger (07c) + authZ (07a) tetap out-of-scope per phase doc.
- **Cross-process rail IDLE di Local** (E2E via test 1-proses invoke-langsung, sama Reporting).

**Touchpoint cert:** AZ-204 — Azure Functions + Service Bus (pattern; serverless branded 05d). PCD — Cloud Run + Pub/Sub push (pattern; branded 06d).
