using FluentValidation;

namespace Wms.MasterData.Application.Features.CreateLocation;

// What: input validation (FluentValidation) — warehouseId & code wajib (fail-fast input shape)
public sealed class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationValidator()
    {
        RuleFor(command => command.WarehouseId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty();
    }
}
