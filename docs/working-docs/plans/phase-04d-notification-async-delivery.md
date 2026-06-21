# Phase 04d — Notification: Async Delivery + Idempotency + Retry/DLQ

**Status:** planned

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

**Handoff notes:** Notification hidup; subscription+delivery+worker+DLQ siap; in-app delivery dipakai WebUI di 04e. Pola pure-consumer + worker sets up Azure Functions (05d) / Cloud Run + Pub/Sub push (06d).

**Touchpoint cert:** AZ-204 — Azure Functions + Service Bus (pattern; serverless branded 05d). PCD — Cloud Run + Pub/Sub push (pattern; branded 06d).
