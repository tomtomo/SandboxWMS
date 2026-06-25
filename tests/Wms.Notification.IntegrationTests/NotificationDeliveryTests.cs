using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Application.Notification;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inbound.Contracts;
using Wms.Inventory.Contracts;
using Wms.Notification.Directory;
using Wms.Notification.Domain;
using Wms.Notification.Endpoints;
using Wms.Notification.Handlers;
using Wms.Notification.Persistence;
using Wms.Notification.Worker;
using Wms.Outbound.Contracts;
using Wms.TestSupport;

namespace Wms.Notification.IntegrationTests;

// What: integration test Notification (DoD Phase 04d, ADR-0017) atas Postgres NYATA (Testcontainers)
// Why: membuktikan async delivery mechanism: (a) event → enqueue NotificationDelivery → worker SEND (Sent);
// (b) idempotency — duplicate event tak double-enqueue (Inbox), worker tak re-send yang Sent; (c) failed
// send → retry → DLQ setelah max attempt; (d) OverDelivery → subscription purchasing; (e) in-app mark-as-read
// via REST; (f) picking → operator DIRECT (recipient dari payload). How: WebApplicationFactory<Program> host
// Notification = container DI + server HTTP; notifier & worker.ProcessOnceAsync di-invoke LANGSUNG
// (deterministik, hosted worker dimatikan); IUserDirectory/IWarehouseDirectory + channel Email di-stub.
[Collection(PostgresCollection.Name)]
public sealed class NotificationDeliveryTests(PostgresFixture fixture)
{
    private const string Warehouse = "WH-JKT";
    private static readonly DateTimeOffset At = new(2026, 6, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Event_enqueues_then_worker_sends_once_and_is_idempotent()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedSubscriptionAsync(
            factory, SubscriberType.User, "spv-1", GRConfirmedV1.LogicalName, [NotificationChannel.InApp], Warehouse);

        var eventId = Guid.NewGuid();
        var message = GrConfirmed();

        await InvokeGrConfirmedAsync(factory, eventId, message);
        Assert.Equal(1, await QueryAsync(factory, db =>
            db.Deliveries.CountAsync(d => d.Status == DeliveryStatus.Pending)));

        // worker dispatch → Sent (1 terkirim)
        Assert.Equal(1, await RunWorkerAsync(factory));
        var sent = await QueryAsync(factory, db => db.Deliveries.SingleAsync());
        Assert.Equal(DeliveryStatus.Sent, sent.Status);
        Assert.NotNull(sent.ProviderMessageId);

        // worker LAGI → tak re-send (idempotent: sudah Sent, di-skip) — 0 dispatched
        Assert.Equal(0, await RunWorkerAsync(factory));

        // re-delivery event yang SAMA (eventId identik) → handler Inbox-dedup → tak enqueue baru
        await InvokeGrConfirmedAsync(factory, eventId, message);
        Assert.Equal(1, await QueryAsync(factory, db => db.Deliveries.CountAsync()));
        Assert.Equal(1, await QueryAsync(factory, db => db.Set<InboxMessage>()
            .CountAsync(i => i.HandlerType == GoodsReceiptConfirmedNotifier.HandlerType)));
    }

    [Fact]
    public async Task Failed_send_retries_then_dead_letters_after_max_attempts()
    {
        var failingEmail = new RecordingEmailSender { ShouldFail = true };
        await using var factory = await CreateFactoryAsync(failingEmail);
        await SeedSubscriptionAsync(
            factory, SubscriberType.User, "spv-1", GRConfirmedV1.LogicalName, [NotificationChannel.Email], Warehouse);

        await InvokeGrConfirmedAsync(factory, Guid.NewGuid(), GrConfirmed());

        // MaxAttempts=3 → tiga tick dispatch, tiap-nya gagal; tick ke-3 meng-exhaust retry → DLQ
        for (var tick = 0; tick < 3; tick++)
            await RunWorkerAsync(factory);

        var delivery = await QueryAsync(factory, db => db.Deliveries.SingleAsync());
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(3, delivery.RetryCount);

        var deadLetter = await QueryAsync(factory, db => db.Set<DeadLetterMessage>().SingleAsync());
        Assert.Equal("notification-worker:Email", deadLetter.Source);

        // tick lagi → delivery exhausted tak di-pick → tak ada DLQ tambahan / tak ada kirim
        Assert.Equal(0, await RunWorkerAsync(factory));
        Assert.Equal(1, await QueryAsync(factory, db => db.Set<DeadLetterMessage>().CountAsync()));
    }

    [Fact]
    public async Task OverDelivery_notifies_purchasing_subscription()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedSubscriptionAsync(
            factory, SubscriberType.User, "spv-1", GRConfirmedV1.LogicalName, [NotificationChannel.InApp], Warehouse);
        await SeedSubscriptionAsync(
            factory, SubscriberType.User, "purchasing-1",
            GoodsReceiptConfirmedNotifier.OverDeliveryEventType, [NotificationChannel.Email], Warehouse);

        // rejectedLines reason=RejectExcess = excess over-delivery (overview §A4)
        var message = new GRConfirmedV1(Guid.NewGuid(), Warehouse, "SUP-1",
            [new ReceivedLineV1("SKU-1", 10, "Good", "B1", null)],
            [new RejectedLineV1("SKU-1", 5, "RejectExcess")]);

        await InvokeGrConfirmedAsync(factory, Guid.NewGuid(), message);

        var deliveries = await QueryAsync(factory, db => db.Deliveries.ToListAsync());
        Assert.Equal(2, deliveries.Count);
        Assert.Contains(deliveries, d =>
            d.UserId == "purchasing-1" && d.EventType == GoodsReceiptConfirmedNotifier.OverDeliveryEventType);
        Assert.Contains(deliveries, d =>
            d.UserId == "spv-1" && d.EventType == GRConfirmedV1.LogicalName);
    }

    [Fact]
    public async Task InApp_delivery_marks_as_read_via_endpoint()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedSubscriptionAsync(
            factory, SubscriberType.User, "spv-1", GRConfirmedV1.LogicalName, [NotificationChannel.InApp], Warehouse);
        await InvokeGrConfirmedAsync(factory, Guid.NewGuid(), GrConfirmed());
        await RunWorkerAsync(factory);

        var delivery = await QueryAsync(factory, db => db.Deliveries.SingleAsync());
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);

        var client = factory.CreateClient();
        var readResponse = await client.PostAsync(
            $"/notifications/deliveries/{delivery.Id.Value}/read", content: null);
        Assert.Equal(HttpStatusCode.NoContent, readResponse.StatusCode);

        var result = await client.GetFromJsonAsync<PagedResult<InAppNotificationRow>>("/notifications/inbox?userId=spv-1");
        var row = Assert.Single(result!.Items);
        Assert.Equal("Read", row.Status);
    }

    [Fact]
    public async Task PickingCompleted_enqueues_direct_operator_delivery()
    {
        await using var factory = await CreateFactoryAsync();

        // recipient DIRECT dari payload (OperatorId) — tanpa subscription
        var message = new PickingCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "B1", 5, "STG-1", "op-9");
        await InvokePickingCompletedAsync(factory, Guid.NewGuid(), message);

        var delivery = await QueryAsync(factory, db => db.Deliveries.SingleAsync());
        Assert.Equal("op-9", delivery.UserId);
        Assert.Null(delivery.SubscriptionId);
        Assert.Equal(NotificationChannel.InApp, delivery.Channel);

        Assert.Equal(1, await RunWorkerAsync(factory));
        Assert.Equal(DeliveryStatus.Sent, (await QueryAsync(factory, db => db.Deliveries.SingleAsync())).Status);
    }

    [Fact]
    public async Task StockAllocationShortfall_notifies_subscribers()
    {
        await using var factory = await CreateFactoryAsync();
        // ADR-0034: event tak bawa warehouseId → subscribe lintas-warehouse (warehouseScope null)
        await SeedSubscriptionAsync(
            factory, SubscriberType.User, "spv-1",
            StockAllocationShortfallV1.LogicalName, [NotificationChannel.InApp], warehouseScope: null);

        var message = new StockAllocationShortfallV1(
            Guid.NewGuid(), [new StockAllocationShortfallLineV1(Guid.NewGuid(), "SKU-1", 10, 4, 6)]);
        await InvokeStockAllocationShortfallAsync(factory, Guid.NewGuid(), message);

        var delivery = await QueryAsync(factory, db => db.Deliveries.SingleAsync());
        Assert.Equal("spv-1", delivery.UserId);
        Assert.Equal(StockAllocationShortfallV1.LogicalName, delivery.EventType);
        Assert.Equal(NotificationChannel.InApp, delivery.Channel);

        Assert.Equal(1, await RunWorkerAsync(factory));   // worker dispatch → Sent
        Assert.Equal(DeliveryStatus.Sent, (await QueryAsync(factory, db => db.Deliveries.SingleAsync())).Status);
    }

    // ---- harness ----

    private static GRConfirmedV1 GrConfirmed() => new(
        Guid.NewGuid(), Warehouse, "SUP-1",
        [new ReceivedLineV1("SKU-1", 10, "Good", "B1", null)],
        []);

    private static async Task SeedSubscriptionAsync(
        WebApplicationFactory<Program> factory, SubscriberType type, string subscriberId,
        string eventType, NotificationChannel[] channels, string? warehouseScope)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var subscription = NotificationSubscription.Create(
            NotificationSubscriptionId.New(), type, subscriberId, eventType, channels, warehouseScope).Value;
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
    }

    private static async Task InvokeGrConfirmedAsync(
        WebApplicationFactory<Program> factory, Guid eventId, GRConfirmedV1 message)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<GoodsReceiptConfirmedNotifier>().HandleAsync(eventId, At, message);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
    }

    private static async Task InvokePickingCompletedAsync(
        WebApplicationFactory<Program> factory, Guid eventId, PickingCompletedV1 message)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<PickingCompletedNotifier>().HandleAsync(eventId, At, message);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
    }

    private static async Task InvokeStockAllocationShortfallAsync(
        WebApplicationFactory<Program> factory, Guid eventId, StockAllocationShortfallV1 message)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<StockAllocationShortfallNotifier>().HandleAsync(eventId, At, message);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
    }

    // worker satu siklus — invoke langsung (deterministik, hosted service dimatikan di test)
    private static Task<int> RunWorkerAsync(WebApplicationFactory<Program> factory)
        => factory.Services.GetRequiredService<NotificationDispatcher>().ProcessOnceAsync();

    private static async Task<T> QueryAsync<T>(
        WebApplicationFactory<Program> factory, Func<NotificationDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<NotificationDbContext>());
    }

    private async Task<NotificationFactory> CreateFactoryAsync(IEmailSender? emailSender = null)
    {
        var factory = new NotificationFactory(await fixture.CreateDatabaseAsync(), emailSender);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
        return factory;
    }

    // WebApplicationFactory host Notification — override connection string ke test DB; stub directory
    // (gRPC tak ada di test) + opsional channel Email (failure injection); matikan worker hosted (ProcessOnceAsync
    // di-invoke manual → deterministik, tak balapan).
    private sealed class NotificationFactory(string connectionString, IEmailSender? emailSender)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:notificationdb", connectionString);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUserDirectory>();
                services.AddSingleton<IUserDirectory, FakeUserDirectory>();
                services.RemoveAll<IWarehouseDirectory>();
                services.AddSingleton<IWarehouseDirectory, FakeWarehouseDirectory>();
                if (emailSender is not null)
                {
                    services.RemoveAll<IEmailSender>();
                    services.AddSingleton(emailSender);
                }
            });
        }
    }
}
