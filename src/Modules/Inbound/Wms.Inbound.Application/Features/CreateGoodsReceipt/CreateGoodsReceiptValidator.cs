using FluentValidation;

namespace Wms.Inbound.Application.Features.CreateGoodsReceipt;

// What: input validation (FluentValidation) — fail-fast SEBELUM transaksi (ADR-0019)
// Why: menolak bentuk request yang jelas invalid lebih awal (warehouse/lines kosong, qty ≤ 0)
// sebagai Result(Validation) di ValidationBehavior — terpisah dari invariant domain yang tetap
// jadi guard TERAKHIR di GoodsReceipt.Create. Validator = input shape; aggregate = invariant bisnis.
// How: ditemukan AddValidatorsFromAssembly; ValidationBehavior menjalankannya sebelum handler.
public sealed class CreateGoodsReceiptValidator : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptValidator()
    {
        RuleFor(command => command.WarehouseId).NotEmpty();
        RuleFor(command => command.Lines).NotEmpty();
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(entry => entry.Sku).NotEmpty();
            line.RuleFor(entry => entry.Quantity).GreaterThan(0);
        });
    }
}
