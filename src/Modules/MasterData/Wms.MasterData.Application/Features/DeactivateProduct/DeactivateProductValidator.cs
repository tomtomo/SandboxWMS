using FluentValidation;

namespace Wms.MasterData.Application.Features.DeactivateProduct;

// What: input validation (FluentValidation) — sku wajib non-empty
// Why: input-shape di-fail-fast ValidationBehavior sebelum bisnis; invariant (Product ada, transisi
// active→inactive legal) tetap ditegakkan domain (Product.Deactivate) — validator melengkapi, bukan
// menggantikan. Tanpa ini, Sku kosong lolos ke handler (soft-delete sensitif, ADR-0014).
public sealed class DeactivateProductValidator : AbstractValidator<DeactivateProductCommand>
{
    public DeactivateProductValidator()
    {
        RuleFor(command => command.Sku).NotEmpty();
    }
}
