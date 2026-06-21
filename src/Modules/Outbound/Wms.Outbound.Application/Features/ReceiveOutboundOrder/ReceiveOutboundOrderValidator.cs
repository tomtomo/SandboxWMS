using FluentValidation;

namespace Wms.Outbound.Application.Features.ReceiveOutboundOrder;

// What: input validation (FluentValidation) — fail-fast shape sebelum bisnis (ValidationBehavior)
// Why: customer/shipTo/lines shape di-fail-fast; legalitas domain (factory invariant) tetap di aggregate.
public sealed class ReceiveOutboundOrderValidator : AbstractValidator<ReceiveOutboundOrderCommand>
{
    public ReceiveOutboundOrderValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.ShipTo).NotEmpty();
        RuleFor(command => command.Lines).NotEmpty();
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(entry => entry.Sku).NotEmpty();
            line.RuleFor(entry => entry.Qty).GreaterThan(0);
        });
    }
}
