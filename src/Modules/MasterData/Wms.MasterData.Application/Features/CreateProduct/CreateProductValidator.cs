using FluentValidation;

namespace Wms.MasterData.Application.Features.CreateProduct;

// What: input validation (FluentValidation) — sku/name/uom wajib non-empty
// Why: input-shape di-fail-fast ValidationBehavior sebelum bisnis; invariant penuh (mis. shelfLifeDays
// positif) tetap ditegakkan domain (Product.Create) — validator melengkapi, bukan menggantikan.
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(command => command.Sku).NotEmpty();
        RuleFor(command => command.Name).NotEmpty();
        RuleFor(command => command.Uom).NotEmpty();
    }
}
