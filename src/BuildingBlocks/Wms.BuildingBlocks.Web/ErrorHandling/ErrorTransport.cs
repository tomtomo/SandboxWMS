using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Web.ErrorHandling;

// What: SATU tabel mapping Error.Type → transport (ADR-0019)
// Why: REST (ProblemDetails) & gRPC (StatusCode) HARUS sepakat atas semantik tiap ErrorType —
// satu sumber kebenaran mencegah drift bila nilai ke-6 ditambah. ToProblemDetails &
// ResultExceptionInterceptor sama-sama memanggil tabel ini, bukan menyalin switch-nya.
// How: satu switch per sumbu (HTTP status, gRPC StatusCode, title); kelima ErrorType
// dipetakan eksplisit, default jatuh ke 500/Internal (fail-safe).
public static class ErrorTransport
{
    // Error.Type → HTTP status (RFC 7807 ProblemDetails.Status)
    public static int ToHttpStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };

    // Error.Type → gRPC StatusCode (ADR-0006: gRPC antar-service)
    public static StatusCode ToGrpcStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCode.InvalidArgument,
        ErrorType.NotFound => StatusCode.NotFound,
        ErrorType.Conflict => StatusCode.FailedPrecondition,
        ErrorType.Unauthorized => StatusCode.Unauthenticated,
        ErrorType.Unexpected => StatusCode.Internal,
        _ => StatusCode.Internal,
    };

    // Error.Type → judul ProblemDetails (RFC 7807 "title": ringkas & stabil per-tipe)
    public static string ToTitle(ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation Failed",
        ErrorType.NotFound => "Resource Not Found",
        ErrorType.Conflict => "Conflict",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Unexpected => "Unexpected Error",
        _ => "Unexpected Error",
    };
}
