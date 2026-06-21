using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Results;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Features.DeactivateProduct;

// What: CQRS — Command Handler (MediatR) — soft-delete Product (overview §D, ADR-0014)
// How: load (repo Get = global filter aktif) → Deactivate (guard) → SaveChanges. Product yang sudah
// inactive tak ter-load (filter) → NotFound (idempotent-aman dari sisi konsumen).
public sealed class DeactivateProductHandler(IProductRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateProductCommand, Result>
{
    public async Task<Result> Handle(DeactivateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await repository.GetAsync(new ProductId(command.Sku), cancellationToken);
        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        var result = product.Deactivate();
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
