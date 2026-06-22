using System.Text.Json;
using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Contracts;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Features.DispatchWave;

// What: CQRS — Command Handler (MediatR) + domain→integration event translation (ADR-0005) — overview §C6
// Why: use-case mengoordinasi Wave Ready→Dispatched (raise ShipmentDispatched, single-aggregate fact) DAN
// menutup tiap OutboundOrder wave (InProgress→Closed) dalam SATU transaksi. ShipmentDispatched (domain) →
// ShipmentDispatchedV1 (published language) → Outbox: Inventory remove Stock Picked terikat wave. Tipe
// domain tak jadi wire-contract (ADR-0009). No-throw (ADR-0019); transaksi state+outbox commit atomic.
// How: load wave → Dispatch() → translate event → Enqueue Outbox → ClearDomainEvents → load orders by wave →
// Close tiap-nya → SaveChanges. Wave belum Ready / order bukan InProgress = Result.Failure (rollback).
public sealed class DispatchWaveHandler(
    IWaveRepository waveRepository,
    IOutboundOrderRepository orderRepository,
    IIntegrationEventOutbox outbox,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DispatchWaveCommand, Result>
{
    public async Task<Result> Handle(DispatchWaveCommand command, CancellationToken cancellationToken)
    {
        var wave = await waveRepository.GetByIdAsync(new WaveId(command.WaveId), cancellationToken);
        if (wave is null)
            return Result.Failure(WaveErrors.NotFound);

        var dispatch = wave.Dispatch();
        if (dispatch.IsFailure)
            return dispatch;

        foreach (var dispatched in wave.DomainEvents.OfType<ShipmentDispatched>())
            outbox.Enqueue(ToEnvelope(dispatched));
        wave.ClearDomainEvents();

        var orders = await orderRepository.ListByWaveAsync(command.WaveId, cancellationToken);
        foreach (var order in orders)
        {
            var close = order.Close();
            if (close.IsFailure)
                return close;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // What: Message Translator (EIP) — domain event → integration event envelope (ADR-0005)
    // How: ShipmentDispatched (domain) → ShipmentDispatchedV1 (contract, hanya waveId); EventId baru =
    // identitas outbox/idempotency (Inventory dedup via Inbox → remove Stock Picked terikat wave).
    private static MessageEnvelope ToEnvelope(ShipmentDispatched dispatched)
    {
        var payload = new ShipmentDispatchedV1(dispatched.WaveId.Value);
        return new MessageEnvelope(
            EventId: Guid.NewGuid(),
            LogicalName: ShipmentDispatchedV1.LogicalName,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: JsonSerializer.Serialize(payload),
            Traceparent: null,
            Tracestate: null);
    }
}
