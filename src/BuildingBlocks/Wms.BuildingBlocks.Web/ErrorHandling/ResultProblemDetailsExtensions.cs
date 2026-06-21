using Microsoft.AspNetCore.Http;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Web.ErrorHandling;

// What: mapping Result/Error → RFC 7807 ProblemDetails (ADR-0019; MS Learn ProblemDetails)
// Why: kegagalan bisnis (Result.Failure) dipetakan SEKALI ke kontrak HTTP standar — endpoint
// tak menyusun bentuk error ad-hoc. Error.Code → "type" (URN stabil) + extension; Error.Message
// → "detail"; Error.Type → status + title via ErrorTransport (satu tabel).
// How: extension dipakai endpoint di cabang gagal (result.ToProblemDetails()); sukses tetap
// ditangani endpoint sendiri (Created/NoContent/Ok).
public static class ResultProblemDetailsExtensions
{
    public static IResult ToProblemDetails(this Result result) => result.IsSuccess
        ? throw new InvalidOperationException("ToProblemDetails hanya untuk Result yang gagal.")
        : result.Error.ToProblemDetails();

    public static IResult ToProblemDetails(this Error error) => Results.Problem(
        statusCode: ErrorTransport.ToHttpStatusCode(error.Type),
        title: ErrorTransport.ToTitle(error.Type),
        detail: error.Message,
        type: $"urn:wms:error:{error.Code}",
        extensions: new Dictionary<string, object?>
        {
            ["errorCode"] = error.Code,
            ["errorType"] = error.Type.ToString(),
        });
}
