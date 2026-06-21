using FluentValidation;

namespace Wms.Outbound.Application.Features.DispatchWave;

// What: input validation (FluentValidation) — waveId wajib non-empty
// Why: shape (waveId terisi) di-fail-fast; legalitas (wave harus Ready) tetap urusan domain (Wave.Dispatch).
public sealed class DispatchWaveValidator : AbstractValidator<DispatchWaveCommand>
{
    public DispatchWaveValidator()
    {
        RuleFor(command => command.WaveId).NotEmpty();
    }
}
