using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Contracts;
using Wms.Inventory.Contracts;
using Wms.Outbound.Contracts;
using Wms.Reporting.Projectors;

namespace Wms.Reporting.Messaging;

// What: in-proc integration-event dispatcher (consumer endpoint; ADR-0005/0029) — sisi Reporting
// Why: menerima MessageEnvelope dari rail (Local: InMemoryMessagePublisher; cloud: adapter broker), route
// by LogicalName → projector yang tepat. INILAH subscribe-point yang disambung adapter broker di Phase
// 05d/06d (Reporting profil serverless event-triggered). Reporting meng-consume EMPAT event lintas-context:
// gr_confirmed (Inbound), stock_removed + putaway_completed (Inventory), picking_completed (Outbound).
// How: helper generik DispatchAsync<TMessage,TProjector> — filter LogicalName → deserialize → buka scope
// (projector + DbContext scoped per pesan) → HandleAsync(eventId, OccurredAt, msg) → throw bila Failure
// (pipeline DLQ). OccurredAt diteruskan dari envelope (bukan wall-clock) → bucket periode rebuild-deterministik.
public sealed class ReportingIntegrationEventDispatcher(IServiceScopeFactory scopeFactory)
{
    public Task HandleGoodsReceiptConfirmedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<GRConfirmedV1, GoodsReceiptConfirmedProjector>(
            envelope, GRConfirmedV1.LogicalName, (p, id, at, msg, ct) => p.HandleAsync(id, at, msg, ct), cancellationToken);

    public Task HandleStockRemovedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<StockRemovedV1, StockRemovedProjector>(
            envelope, StockRemovedV1.LogicalName, (p, id, at, msg, ct) => p.HandleAsync(id, at, msg, ct), cancellationToken);

    public Task HandlePutawayCompletedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<PutawayCompletedV1, PutawayCompletedProjector>(
            envelope, PutawayCompletedV1.LogicalName, (p, id, at, msg, ct) => p.HandleAsync(id, at, msg, ct), cancellationToken);

    public Task HandlePickingCompletedAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync<PickingCompletedV1, PickingCompletedProjector>(
            envelope, PickingCompletedV1.LogicalName, (p, id, at, msg, ct) => p.HandleAsync(id, at, msg, ct), cancellationToken);

    private async Task DispatchAsync<TMessage, TProjector>(
        MessageEnvelope envelope,
        string logicalName,
        Func<TProjector, Guid, DateTimeOffset, TMessage, CancellationToken, Task<Result>> handle,
        CancellationToken cancellationToken)
        where TProjector : notnull
    {
        if (envelope.LogicalName != logicalName)
            return;

        var message = JsonSerializer.Deserialize<TMessage>(envelope.Payload)
            ?? throw new InvalidOperationException(
                $"Payload {logicalName} (event {envelope.EventId}) gagal di-deserialize.");

        using var scope = scopeFactory.CreateScope();
        var projector = scope.ServiceProvider.GetRequiredService<TProjector>();

        var result = await handle(projector, envelope.EventId, envelope.OccurredAt, message, cancellationToken);
        if (result.IsFailure)
            throw new InvalidOperationException(
                $"Projector {logicalName} gagal (event {envelope.EventId}): {result.Error.Code} — {result.Error.Message}");
    }
}
