using FluentValidation;

namespace Wms.Auth.Application.Features.Login;

// What: input validation (FluentValidation) — username/password wajib non-empty
// Why: input-shape di-fail-fast ValidationBehavior sebelum bisnis. CATATAN: validator hanya menolak
// input KOSONG (shape) — kegagalan kredensial (user tak ada / password salah) ditangani handler dengan
// jalur timing-safe SERAGAM (anti user-enumeration, ADR-0016), bukan di sini.
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Username).NotEmpty();
        RuleFor(command => command.Password).NotEmpty();
    }
}
