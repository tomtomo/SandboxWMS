using FluentValidation;

namespace Wms.Outbound.Application.Features.CreateWave;

// What: input validation (FluentValidation) — wave minimal punya satu order
// Why: shape (orderIds non-empty) di-fail-fast; legalitas (order harus New, wave ≥1 order) tetap di domain.
public sealed class CreateWaveValidator : AbstractValidator<CreateWaveCommand>
{
    public CreateWaveValidator()
    {
        RuleFor(command => command.OrderIds).NotEmpty();
    }
}
