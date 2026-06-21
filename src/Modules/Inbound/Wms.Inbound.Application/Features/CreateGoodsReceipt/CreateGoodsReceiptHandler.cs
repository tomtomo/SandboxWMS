using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.CreateGoodsReceipt;

// What: CQRS — Command Handler (MediatR) — write path lewat aggregate (ADR-0004)
// Why: satu-satunya tempat yang membuka GoodsReceipt; mengembalikan Result (no-throw-for-business,
// ADR-0019); transaksi & rollback dikelola TransactionBehavior. Tak emit event (Create bukan fakta
// yang dipublish, ADR-0026). uom di-SNAPSHOT dari MasterData read-API (ADR-0014/0011) via IProductCatalog
// (ACL) — MENGGANTIKAN uom yang dulu disuplai caller/seed; sku tak dikenal → gagal (tak bisa snapshot
// uom produk asing).
// How: per line resolve Product master (gRPC + cache-aside di MasterData) → ExpectedLineInput(uom master)
// → factory Create (invariant → Result) → persist via repository port → SaveChanges (commit di pipeline).
public sealed class CreateGoodsReceiptHandler(
    IProductCatalog productCatalog,
    IGoodsReceiptRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateGoodsReceiptCommand, Result<Guid>>
{
    // sku di luar katalog MasterData = data/contract error → Validation (→400)
    private static readonly Error UnknownProduct =
        Error.Validation("inbound.unknown_product", "Product (sku) tidak dikenal di MasterData.");

    public async Task<Result<Guid>> Handle(
        CreateGoodsReceiptCommand command, CancellationToken cancellationToken)
    {
        var expectedLines = new List<ExpectedLineInput>(command.ExpectedLines.Count);
        foreach (var line in command.ExpectedLines)
        {
            var product = await productCatalog.GetProductAsync(line.Sku, cancellationToken);
            if (product is null)
                return Result.Failure<Guid>(UnknownProduct);

            expectedLines.Add(new ExpectedLineInput(line.Sku, line.ExpectedQty, product.Uom));
        }

        var result = GoodsReceipt.Create(
            GoodsReceiptId.New(), command.WarehouseId, expectedLines,
            command.PoRef, command.SupplierId, command.DockDoor);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await repository.AddAsync(result.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(result.Value.Id.Value);
    }
}
