using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Contracts;
using Wms.Outbound.Application.Features.ConsumeStockAllocated;
using Wms.Outbound.Application.Features.ConsumeStockAllocationFailed;

namespace Wms.Outbound.Infrastructure.Messaging;

// What: in-proc integration-event dispatcher (consumer endpoint; ADR-0005 / ADR-0029)
// Why: menerima MessageEnvelope dari rail (Local: InMemoryMessagePublisher; cloud: adapter broker), route
// by LogicalName → consumer yang tepat. INILAH subscribe-point yang disambung adapter broker di Phase 05/06.
// Phase 03c: Outbound meng-consume SATU event (StockAllocated → buat PickingTask). Method publik per-event
// agar host bisa men-subscribe-kannya dengan DLQ source (HandlerType) granular per consumer (forensik tepat).
// How: helper generik DispatchAsync<TMessage, TConsumer> — filter LogicalName → deserialize → buka scope
// (consumer + DbContext scoped per pesan) → HandleAsync → throw bila Result.Failure (pipeline DLQ).
public sealed class OutboundIntegrationEventDispatcher(IServiceScopeFactory scopeFactory)
{
    public Task HandleStockAllocatedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<StockAllocatedV1, StockAllocatedConsumer>(
            envelope, StockAllocatedV1.LogicalName, (consumer, id, msg, ct) => consumer.HandleAsync(id, msg, ct), cancellationToken);

    // ADR-0034: sinyal-gagal alokasi → tandai OrderLine Short/Backordered
    public Task HandleStockAllocationFailedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<StockAllocationFailedV1, StockAllocationFailedConsumer>(
            envelope, StockAllocationFailedV1.LogicalName, (consumer, id, msg, ct) => consumer.HandleAsync(id, msg, ct), cancellationToken);

    // What: routing generik — filter logical name, terjemahkan payload, jalankan consumer dalam scope.
    // Why: satu definisi dispatch (deserialize + scope + error→throw) — rail Local fan-out ke SEMUA subscriber,
    // jadi method abaikan envelope yang bukan miliknya (return cepat); sukses (no-throw) → tak ter-dead-letter keliru.
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
