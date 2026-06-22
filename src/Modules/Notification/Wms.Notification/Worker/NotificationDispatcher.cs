using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Application.Notification;
using Wms.Notification.Directory;
using Wms.Notification.Domain;
using Wms.Notification.Persistence;

namespace Wms.Notification.Worker;

// What: BackgroundService — async delivery worker (overview §G; ADR-0017)
// Why: delivery dipisah dari event-handler (decoupling) supaya flow event utama TAK ke-block channel
// provider. Worker resolve recipient (Auth read-API) + warehouse context (MasterData read-API) lalu
// dispatch ke channel; IDEMPOTENT (skip yang sudah Sent); kegagalan channel di-RETRY in-line, exhausted →
// Dead Letter Channel (EIP; reuse IDeadLetterStore). Sisi-konsumen worker, kembaran OutboxDispatcher.
// How: ExecuteAsync loop poll → ProcessOnceAsync (scope baru per tick). ProcessOnceAsync dapat di-invoke
// LANGSUNG di integration test (deterministik, tanpa balapan background) — sama pola OutboxDispatcher.
public sealed class NotificationDispatcher(
    IServiceScopeFactory scopeFactory,
    NotificationDeliveryOptions options,
    ILogger<NotificationDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Notification dispatch tick gagal; retry di interval berikutnya.");
            }

            try
            {
                await Task.Delay(options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // What: satu siklus dispatch — pickup deliverable, kirim, transisi state (testable langsung)
    // Why: query "Pending DULU + Failed yang belum exhausted", urut QueuedAt → fairness + bounded batch.
    // Per delivery di-commit TERPISAH (bukan batch) → kegagalan satu tak rollback yang lain; retry/DLQ crisp.
    public async Task<int> ProcessOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<NotificationDbContext>();

        var batch = await db.Deliveries
            .Where(delivery => delivery.Status == DeliveryStatus.Pending
                || (delivery.Status == DeliveryStatus.Failed && delivery.RetryCount < options.MaxAttempts))
            .OrderBy(delivery => delivery.QueuedAt)
            .Take(options.BatchSize)
            .ToListAsync(cancellationToken);

        var dispatched = 0;
        foreach (var delivery in batch)
        {
            // idempotency (ADR-0017): re-delivery/replay yang sudah final tak boleh dikirim ulang
            if (delivery.IsAlreadyDispatched)
                continue;

            try
            {
                var providerMessageId = await DispatchAsync(delivery, services, cancellationToken);
                delivery.MarkSent(providerMessageId, DateTimeOffset.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                dispatched++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                delivery.MarkFailed(ex.Message);
                await db.SaveChangesAsync(cancellationToken);

                logger.LogWarning(ex,
                    "Delivery {DeliveryId} ({Channel}) gagal, attempt {Attempt}/{MaxAttempts}.",
                    delivery.Id.Value, delivery.Channel, delivery.RetryCount, options.MaxAttempts);

                if (delivery.HasExhaustedRetries(options.MaxAttempts))
                    await DeadLetterAsync(services, delivery, ex, cancellationToken);
            }
        }

        return dispatched;
    }

    // What: resolusi recipient/warehouse (read-API gRPC) + dispatch ke channel port (ADR-0011/0017)
    // Why: Email butuh alamat → Auth read-API; semua channel diperkaya nama warehouse → MasterData read-API
    // (enrichment, null-tolerant). Pemilihan channel port = channel-provider abstraction (swap adapter).
    private static async Task<string> DispatchAsync(
        NotificationDelivery delivery, IServiceProvider services, CancellationToken cancellationToken)
    {
        var body = delivery.Body;
        if (delivery.WarehouseId is not null)
        {
            var warehouse = await services.GetRequiredService<IWarehouseDirectory>()
                .GetWarehouseAsync(delivery.WarehouseId, cancellationToken);
            if (warehouse is not null)
                body = $"{body} (warehouse: {warehouse.Name})";
        }

        switch (delivery.Channel)
        {
            case NotificationChannel.Email:
                var contact = await services.GetRequiredService<IUserDirectory>()
                    .GetUserAsync(delivery.UserId, cancellationToken);
                if (contact is null || string.IsNullOrWhiteSpace(contact.Email))
                    throw new InvalidOperationException($"alamat email user {delivery.UserId} tak ditemukan.");
                return await services.GetRequiredService<IEmailSender>()
                    .SendAsync(contact.Email, delivery.Title, body, cancellationToken);

            case NotificationChannel.Push:
                return await services.GetRequiredService<IPushNotifier>()
                    .SendAsync(delivery.UserId, delivery.Title, body, cancellationToken);

            case NotificationChannel.InApp:
                return await services.GetRequiredService<IInAppNotifier>()
                    .SendAsync(delivery.UserId, delivery.Title, body, cancellationToken);

            default:
                throw new InvalidOperationException($"channel {delivery.Channel} tak didukung.");
        }
    }

    // What: parkir delivery racun ke Dead Letter Channel (forensik) lewat port (EIP; ADR-0005/0017)
    // Why: setelah MaxAttempts, kegagalan channel tak boleh memblok delivery lain selamanya — pindah ke
    // dead_letter (di-correlate ke source event via EventId). Isolasi kegagalan channel provider dari core.
    private static async Task DeadLetterAsync(
        IServiceProvider services, NotificationDelivery delivery, Exception ex, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            deliveryId = delivery.Id.Value,
            delivery.UserId,
            channel = delivery.Channel.ToString(),
            delivery.Title,
        });

        await services.GetRequiredService<IDeadLetterStore>().StoreAsync(new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.TryParse(delivery.EventRef, out var eventId) ? eventId : Guid.Empty,
            LogicalName = delivery.EventType,
            Payload = payload,
            Source = $"notification-worker:{delivery.Channel}",
            Error = ex.Message.Length <= 4096 ? ex.Message : ex.Message[..4096],
            Attempts = delivery.RetryCount,
            DeadLetteredAt = DateTimeOffset.UtcNow,
        }, cancellationToken);
    }
}
