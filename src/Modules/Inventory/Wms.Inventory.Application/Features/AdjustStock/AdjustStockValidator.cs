using FluentValidation;

namespace Wms.Inventory.Application.Features.AdjustStock;

// What: input validation (FluentValidation) — NewQty tak boleh negatif
// Why: input-shape (kuantitas non-negatif) di-fail-fast ValidationBehavior sebelum bisnis; domain
// (Stock.Adjust) tetap menegakkan invariant yang sama sebagai defense-in-depth (no-throw, FF#7).
public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(command => command.NewQty).GreaterThanOrEqualTo(0);
    }
}
