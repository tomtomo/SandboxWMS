using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Contracts;
using Wms.Inventory.Application.Features.ConsumeGoodsReceiptConfirmed;
using Wms.Inventory.Application.Features.ConsumePickingCompleted;
using Wms.Inventory.Application.Features.ConsumeShipmentDispatched;
using Wms.Inventory.Application.Features.ConsumeWaveReleased;
using Wms.Outbound.Contracts;

namespace Wms.Inventory.Infrastructure.Messaging;

// What: in-proc integration-event dispatcher (consumer endpoint; ADR-0005 / ADR-0029)
// Why: menerima MessageEnvelope dari rail (Local: InMemoryMessagePublisher; cloud: adapter broker),
// route by LogicalName → consumer yang tepat. INILAH subscribe-point yang disambung adapter broker di
// Phase 05/06. Phase 03b: Inventory kini meng-consume EMPAT event (GRConfirmed, WaveReleased,
// PickingCompleted, ShipmentDispatched) — tiap event punya method publik sendiri agar host bisa
// men-subscribe-kannya dengan DLQ source (HandlerType) granular per consumer (forensik tepat).
// How: helper generik DispatchAsync<TMessage, TConsumer> — filter LogicalName → deserialize → buka scope
// (consumer + DbContext scoped, satu per pesan) → HandleAsync → throw bila Result.Failure (pipeline DLQ).
public sealed class InventoryIntegrationEventDispatcher(IServiceScopeFactory scopeFactory)
{
    public Task HandleGoodsReceiptConfirmedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<GRConfirmedV1, GoodsReceiptConfirmedConsumer>(
            envelope, GRConfirmedV1.LogicalName, (consumer, id, msg, ct) => consumer.HandleAsync(id, msg, ct), cancellationToken);

    public Task HandleWaveReleasedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<WaveReleasedV1, WaveReleasedConsumer>(
            envelope, WaveReleasedV1.LogicalName, (consumer, id, msg, ct) => consumer.HandleAsync(id, msg, ct), cancellationToken);

    public Task HandlePickingCompletedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<PickingCompletedV1, PickingCompletedConsumer>(
            envelope, PickingCompletedV1.LogicalName, (consumer, id, msg, ct) => consumer.HandleAsync(id, msg, ct), cancellationToken);

    public Task HandleShipmentDispatchedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<ShipmentDispatchedV1, ShipmentDispatchedConsumer>(
            envelope, ShipmentDispatchedV1.LogicalName, (consumer, id, msg, ct) => consumer.HandleAsync(id, msg, ct), cancellationToken);

    // What: routing generik — filter logical name, terjemahkan payload, jalankan consumer dalam scope.
    // Why: satu definisi dispatch (deserialize + scope + error→throw) dipakai ulang 4 event — DRY +
    // konsisten. Filter di sini: rail Local fan-out ke SEMUA subscriber, jadi tiap method abaikan
    // envelope yang bukan miliknya (return cepat) — sukses (no-throw) → tak ter-dead-letter keliru.
    private async Task DispatchAsync<TMessage, TConsumer>(
        MessageEnvelope envelope,
        string logicalName,
        Func<TConsumer, Guid, TMessage, CancellationToken, Task<Result>> handle,
        CancellationToken cancellationToken)
        where TConsumer : notnull
    {
        if (envelope.LogicalName != logicalName)
            return;

        var message = JsonSerializer.Deserialize<TMessage>(envelope.Payload)
            ?? throw new InvalidOperationException(
                $"Payload {logicalName} (event {envelope.EventId}) gagal di-deserialize.");

        using var scope = scopeFactory.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<TConsumer>();

        var result = await handle(consumer, envelope.EventId, message, cancellationToken);
        if (result.IsFailure)
            throw new InvalidOperationException(
                $"Consumer {logicalName} gagal (event {envelope.EventId}): {result.Error.Code} — {result.Error.Message}");
    }
}
