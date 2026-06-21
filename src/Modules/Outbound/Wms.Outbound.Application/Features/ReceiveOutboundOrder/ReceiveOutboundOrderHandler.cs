using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Features.ReceiveOutboundOrder;

// What: CQRS — Command Handler (MediatR) — terima order eksternal → OutboundOrder New (overview §C1)
// Why: uom di-SNAPSHOT dari MasterData read-API (ADR-0014/0011) via IProductCatalog (ACL) —
// MENGGANTIKAN seed lokal Phase 03 (OutboundSeed dihapus). Tiap line resolve Product master; sku tak
// dikenal → gagal (order tak bisa men-snapshot uom produk asing). Invariant (customer/shipTo/lines)
// ditegakkan factory domain; handler merangkai + memetakan kegagalan ke Result (no-throw, ADR-0019).
// How: per line panggil catalog (gRPC + cache-aside di sisi MasterData) → OrderLineInput(uom master) →
// OutboundOrder.Create → AddAsync → SaveChanges → id baru.
public sealed class ReceiveOutboundOrderHandler(
    IProductCatalog productCatalog,
    IOutboundOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ReceiveOutboundOrderCommand, Result<Guid>>
{
    // sku di luar katalog MasterData = data/contract error → Validation (→400)
    private static readonly Error UnknownProduct =
        Error.Validation("outbound.unknown_product", "Product (sku) tidak dikenal di MasterData.");

    public async Task<Result<Guid>> Handle(
        ReceiveOutboundOrderCommand command, CancellationToken cancellationToken)
    {
        var lines = new List<OrderLineInput>(command.Lines.Count);
        foreach (var line in command.Lines)
        {
            var product = await productCatalog.GetProductAsync(line.Sku, cancellationToken);
            if (product is null)
                return Result.Failure<Guid>(UnknownProduct);

            lines.Add(new OrderLineInput(line.Sku, line.Qty, product.Uom));
        }

        var result = OutboundOrder.Create(OutboundOrderId.New(), command.CustomerId, command.ShipTo, lines);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await orderRepository.AddAsync(result.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(result.Value.Id.Value);
    }
}
