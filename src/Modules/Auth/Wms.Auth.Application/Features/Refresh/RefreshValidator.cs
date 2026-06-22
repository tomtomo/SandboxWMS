using FluentValidation;

namespace Wms.Auth.Application.Features.Refresh;

// What: input validation (FluentValidation) — refresh token wajib non-empty
public sealed class RefreshValidator : AbstractValidator<RefreshCommand>
{
    public RefreshValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}
