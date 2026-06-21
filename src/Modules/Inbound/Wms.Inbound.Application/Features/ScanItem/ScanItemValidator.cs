using FluentValidation;

namespace Wms.Inbound.Application.Features.ScanItem;

// What: input validation (FluentValidation) — fail-fast bentuk scan sebelum sentuh aggregate
public sealed class ScanItemValidator : AbstractValidator<ScanItemCommand>
{
    public ScanItemValidator()
    {
        RuleFor(command => command.Sku).NotEmpty();
        RuleFor(command => command.ActualQty).GreaterThan(0);
    }
}
