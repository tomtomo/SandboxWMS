using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Contracts;
using Wms.Inventory.Contracts;
using Wms.Notification.Handlers;
using Wms.Outbound.Contracts;

namespace Wms.Notification.Messaging;

// What: in-proc integration-event dispatcher (consumer endpoint; ADR-0005/0029) — sisi Notification
// Why: menerima MessageEnvelope dari rail (Local: InMemoryMessagePublisher; cloud: adapter broker), route
// by LogicalName → notifier yang tepat. INILAH subscribe-point yang disambung adapter broker di Phase
// 05d/06d (Notification profil serverless/Cloud Run push). Meng-consume TIGA event: gr_confirmed (Inbound) +
// picking_completed (Outbound) + stock_allocation_failed (Inventory, ADR-0034: alert stock kurang untuk wave).
// How: helper generik DispatchAsync<TMessage,THandler> — filter LogicalName → deserialize → buka scope
// (handler + DbContext scoped per pesan) → HandleAsync(eventId, OccurredAt, msg) → throw bila Failure
// (pipeline retry→DLQ ConsumerDeadLetterPipeline). OccurredAt diteruskan dari envelope (bukan wall-clock).
public sealed class NotificationIntegrationEventDispatcher(IServiceScopeFactory scopeFactory)
{
    public Task HandleGoodsReceiptConfirmedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<GRConfirmedV1, GoodsReceiptConfirmedNotifier>(
            envelope, GRConfirmedV1.LogicalName, (h, id, at, msg, ct) => h.HandleAsync(id, at, msg, ct), cancellationToken);

    public Task HandlePickingCompletedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<PickingCompletedV1, PickingCompletedNotifier>(
            envelope, PickingCompletedV1.LogicalName, (h, id, at, msg, ct) => h.HandleAsync(id, at, msg, ct), cancellationToken);

    // ADR-0034: sinyal-gagal alokasi → alert subscriber (stock kurang untuk wave)
    public Task HandleStockAllocationFailedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<StockAllocationFailedV1, StockAllocationFailedNotifier>(
            envelope, StockAllocationFailedV1.LogicalName, (h, id, at, msg, ct) => h.HandleAsync(id, at, msg, ct), cancellationToken);

    private async Task DispatchAsync<TMessage, THandler>(
        MessageEnvelope envelope,
        string logicalName,
        Func<THandler, Guid, DateTimeOffset, TMessage, CancellationToken, Task<Result>> handle,
        CancellationToken cancellationToken)
        where THandler : notnull
    {
        if (envelope.LogicalName != logicalName)
            return;

        var message = JsonSerializer.Deserialize<TMessage>(envelope.Payload)
            ?? throw new InvalidOperationException(
                $"Payload {logicalName} (event {envelope.EventId}) gagal di-deserialize.");

        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();

        var result = await handle(handler, envelope.EventId, envelope.OccurredAt, message, cancellationToken);
        if (result.IsFailure)
            throw new InvalidOperationException(
                $"Notifier {logicalName} gagal (event {envelope.EventId}): {result.Error.Code} — {result.Error.Message}");
    }
}
