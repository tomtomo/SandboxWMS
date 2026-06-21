using FluentValidation;

namespace Wms.Inbound.Application.Features.HoldGoodsReceipt;

// What: input validation (FluentValidation) — holdReason wajib non-empty (input-shape, bukan invariant domain)
public sealed class HoldGoodsReceiptValidator : AbstractValidator<HoldGoodsReceiptCommand>
{
    public HoldGoodsReceiptValidator()
    {
        RuleFor(command => command.Reason).NotEmpty();
    }
}
