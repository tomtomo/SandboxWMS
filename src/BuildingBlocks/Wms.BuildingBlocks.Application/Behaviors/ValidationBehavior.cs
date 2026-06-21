using FluentValidation;
using MediatR;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Application.Behaviors;

// What: Pipeline Behavior (MediatR) — input validation, fail-fast (ADR-0004 amendment; ADR-0019)
// Why: validasi terpusat SEBELUM Transaction/Handler — input invalid tak boleh membuka
// transaksi. Kegagalan dikembalikan sebagai Result(Error.Validation), BUKAN throw
// (no-throw-for-business, ADR-0019), supaya dipetakan seragam ke ProblemDetails 400 di tepi.
// How: jalankan semua IValidator<TRequest>; bila ada failure, short-circuit dengan failure
// Result ber-tipe TResponse (ResultFactory) TANPA memanggil next().
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(validator => validator.Validate(context))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(cancellationToken);

        var message = string.Join("; ", failures.Select(failure => failure.ErrorMessage));
        return ResultFactory<TResponse>.Failure(Error.Validation("validation.failed", message));
    }
}
