using FluentValidation;

namespace Wms.Inventory.Application.Features.CompletePutaway;

// What: input validation (FluentValidation) — actualDestinationId wajib non-empty
// Why: input-shape (rak tujuan harus diisi operator) di-fail-fast ValidationBehavior sebelum bisnis;
// legalitas state (task harus Assigned) tetap urusan domain (PutawayTask.Complete).
public sealed class CompletePutawayValidator : AbstractValidator<CompletePutawayCommand>
{
    public CompletePutawayValidator()
    {
        RuleFor(command => command.ActualDestinationId).NotEmpty();
    }
}
