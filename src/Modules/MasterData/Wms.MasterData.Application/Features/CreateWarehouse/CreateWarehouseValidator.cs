using FluentValidation;

namespace Wms.MasterData.Application.Features.CreateWarehouse;

// What: input validation (FluentValidation) — name/address wajib non-empty (fail-fast input shape)
public sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseValidator()
    {
        RuleFor(command => command.Name).NotEmpty();
        RuleFor(command => command.Address).NotEmpty();
    }
}
