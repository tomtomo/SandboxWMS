using System.Text.Json;
using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Contracts;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Features.CreateWave;

// What: CQRS — Command Handler (MediatR) + cross-aggregate event composition (ADR-0005) — buat wave (§C2)
// Why: use-case mengoordinasi BANYAK aggregate — tiap OutboundOrder New→InProgress + satu Wave Active — dalam
// SATU transaksi. WaveReleased lines[] mengagregasi orderLines LINTAS-order, jadi DIKOMPOSISI di handler
// (bukan domain event per-aggregate) — pola sama dgn StockAllocated di Inventory: application service tempat
// yang tepat menerjemahkan fakta cross-aggregate jadi integration event. Tipe domain tak jadi wire-contract.
// How: load orders by ids → PlaceInWave tiap-nya (guard New) → Wave.Activate → compose WaveReleasedV1 dari
// orderLines → Enqueue Outbox → SaveChanges (state + outbox SATU transaksi, anti dual-write).
public sealed class CreateWaveHandler(
    IOutboundOrderRepository orderRepository,
    IWaveRepository waveRepository,
    IIntegrationEventOutbox outbox,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateWaveCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateWaveCommand command, CancellationToken cancellationToken)
    {
        var orderIds = command.OrderIds.Distinct().ToList();
        var orders = await orderRepository.ListByIdsAsync(orderIds, cancellationToken);
        if (orders.Count != orderIds.Count)
            return Result.Failure<Guid>(OutboundOrderErrors.NotFound);

        var waveId = WaveId.New();
        foreach (var order in orders)
        {
            var placed = order.PlaceInWave(waveId.Value);
            if (placed.IsFailure)
                return Result.Failure<Guid>(placed.Error);
        }

        var waveResult = Wave.Activate(waveId, orderIds);
        if (waveResult.IsFailure)
            return Result.Failure<Guid>(waveResult.Error);

        await waveRepository.AddAsync(waveResult.Value, cancellationToken);
        outbox.Enqueue(ToEnvelope(waveId.Value, orders));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(waveId.Value);
    }

    // What: Message Translator (EIP) — orderLines lintas-order → WaveReleasedV1 envelope (ADR-0005)
    // How: flatten OrderLine tiap order jadi WaveLineV1 (orderId/sku/qty); Inventory pakai sku+qty utk FEFO,
    // orderId untuk korelasi downstream. EventId baru = identitas outbox/idempotency (Inventory dedup via Inbox).
    private static MessageEnvelope ToEnvelope(Guid waveId, IReadOnlyList<OutboundOrder> orders)
    {
        var lines = orders
            .SelectMany(order => order.OrderLines
                .Select(line => new WaveLineV1(order.Id.Value, line.Sku, line.Qty)))
            .ToList();

        var payload = new WaveReleasedV1(waveId, lines);
        return new MessageEnvelope(
            EventId: Guid.NewGuid(),
            LogicalName: WaveReleasedV1.LogicalName,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: JsonSerializer.Serialize(payload),
            Traceparent: null,
            Tracestate: null);
    }
}
