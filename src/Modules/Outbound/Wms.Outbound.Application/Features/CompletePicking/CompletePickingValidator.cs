using FluentValidation;

namespace Wms.Outbound.Application.Features.CompletePicking;

// What: input validation (FluentValidation) — stagingLocationId wajib non-empty
// Why: input-shape (lokasi staging harus diisi operator) di-fail-fast; legalitas state (task harus Assigned)
// tetap urusan domain (PickingTask.Complete).
public sealed class CompletePickingValidator : AbstractValidator<CompletePickingCommand>
{
    public CompletePickingValidator()
    {
        RuleFor(command => command.StagingLocationId).NotEmpty();
    }
}
