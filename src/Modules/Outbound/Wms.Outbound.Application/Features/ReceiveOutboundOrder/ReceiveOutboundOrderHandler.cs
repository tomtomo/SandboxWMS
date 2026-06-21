using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Features.ReceiveOutboundOrder;

// What: CQRS — Command Handler (MediatR) — terima order eksternal → OutboundOrder New (overview §C1)
// Why: satu use-case membuat satu aggregate. uom di-SNAPSHOT dari seed (ADR-0014) — sampai MasterData 04a,
// seed lokal jadi stand-in Product master. Invariant (customer/shipTo/lines) ditegakkan factory domain;
// handler hanya merangkai + memetakan kegagalan ke Result (no-throw, ADR-0019).
// How: bangun OrderLineInput (uom seed) → OutboundOrder.Create → AddAsync → SaveChanges → return id baru.
public sealed class ReceiveOutboundOrderHandler(
    IOutboundOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ReceiveOutboundOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        ReceiveOutboundOrderCommand command, CancellationToken cancellationToken)
    {
        var lines = command.Lines
            .Select(line => new OrderLineInput(line.Sku, line.Qty, OutboundSeed.DefaultUom))
            .ToList();

        var result = OutboundOrder.Create(OutboundOrderId.New(), command.CustomerId, command.ShipTo, lines);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await orderRepository.AddAsync(result.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(result.Value.Id.Value);
    }
}
