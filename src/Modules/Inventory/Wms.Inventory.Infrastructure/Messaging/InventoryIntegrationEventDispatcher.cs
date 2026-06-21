using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.Inbound.Contracts;
using Wms.Inventory.Application.Features.ConsumeGoodsReceiptConfirmed;

namespace Wms.Inventory.Infrastructure.Messaging;

// What: in-proc integration-event dispatcher (consumer endpoint; ADR-0005 / ADR-0029)
// Why: menerima MessageEnvelope dari rail (Local: InMemoryMessagePublisher; cloud: adapter
// broker), route by LogicalName → consumer. INILAH subscribe-point yang di Local 2-proses
// idle, dan disambung adapter broker di Phase 05/06 (ADR-0029). Platform-agnostic: tak tahu
// In-proc vs Service Bus — caller yang men-subscribe-kannya.
// How: filter LogicalName == GRConfirmedV1.LogicalName → deserialize Payload → buka scope
// (consumer + DbContext scoped, satu per pesan) → HandleAsync(envelope.EventId, message).
public sealed class InventoryIntegrationEventDispatcher(IServiceScopeFactory scopeFactory)
{
    public async Task HandleAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (envelope.LogicalName != GRConfirmedV1.LogicalName)
            return;

        var message = JsonSerializer.Deserialize<GRConfirmedV1>(envelope.Payload)
            ?? throw new InvalidOperationException(
                $"Payload {GRConfirmedV1.LogicalName} (event {envelope.EventId}) gagal di-deserialize.");

        using var scope = scopeFactory.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<GoodsReceiptConfirmedConsumer>();

        var result = await consumer.HandleAsync(envelope.EventId, message, cancellationToken);
        if (result.IsFailure)
            throw new InvalidOperationException(
                $"Consumer GRConfirmed gagal (event {envelope.EventId}): {result.Error.Code} — {result.Error.Message}");
    }
}
