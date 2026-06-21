using MediatR;
using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Application.Behaviors;

// What: Pipeline Behavior (MediatR) — structured logging, cross-cutting (ADR-0004 amendment)
// Why: jejak request/outcome tak boleh tersebar di tiap handler; behavior ini terdaftar
// PERTAMA (paling luar) supaya membungkus seluruh pipeline — termasuk hasil short-circuit
// Validation. Outcome dibaca dari Result (IsSuccess / Error.Code), bukan dari exception.
// How: log saat masuk → next() → log outcome; batas TResponse : Result membuat outcome
// terbaca seragam tanpa tahu tipe konkret request.
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {Request}", requestName);

        var response = await next(cancellationToken);

        if (response.IsSuccess)
            logger.LogInformation("Handled {Request} → success", requestName);
        else
            logger.LogWarning("Handled {Request} → failure {ErrorCode} ({ErrorType})",
                requestName, response.Error.Code, response.Error.Type);

        return response;
    }
}
