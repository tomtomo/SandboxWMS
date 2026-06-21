using FluentValidation;

namespace Wms.Inbound.Application.Features.CreateGoodsReceipt;

// What: input validation (FluentValidation) — fail-fast SEBELUM transaksi (ADR-0019)
// Why: menolak bentuk request yang jelas invalid lebih awal (warehouse/lines kosong, qty ≤ 0, uom
// kosong) sebagai Result(Validation) di ValidationBehavior — terpisah dari invariant domain yang
// tetap jadi guard TERAKHIR di GoodsReceipt.Create. Validator = input shape; aggregate = invariant.
public sealed class CreateGoodsReceiptValidator : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptValidator()
    {
        RuleFor(command => command.WarehouseId).NotEmpty();
        RuleFor(command => command.ExpectedLines).NotEmpty();
        RuleForEach(command => command.ExpectedLines).ChildRules(line =>
        {
            line.RuleFor(entry => entry.Sku).NotEmpty();
            line.RuleFor(entry => entry.ExpectedQty).GreaterThan(0);
            line.RuleFor(entry => entry.Uom).NotEmpty();
        });
    }
}
