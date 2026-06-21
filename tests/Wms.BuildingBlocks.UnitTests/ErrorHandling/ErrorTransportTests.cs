using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wms.BuildingBlocks.Domain.Results;
using Wms.BuildingBlocks.Web.ErrorHandling;

namespace Wms.BuildingBlocks.UnitTests.ErrorHandling;

// What: unit test SATU tabel mapping + ToProblemDetails (ADR-0019)
// Why: kelima ErrorType wajib memetakan ke status REST & gRPC yang benar (kontrak transport),
// dan Result.Failure → RFC 7807 ProblemDetails ber-status + kode stabil.
public sealed class ErrorTransportTests
{
    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Unexpected, StatusCodes.Status500InternalServerError)]
    public void Maps_error_type_to_http_status(ErrorType type, int expected)
        => Assert.Equal(expected, ErrorTransport.ToHttpStatusCode(type));

    [Theory]
    [InlineData(ErrorType.Validation, StatusCode.InvalidArgument)]
    [InlineData(ErrorType.NotFound, StatusCode.NotFound)]
    [InlineData(ErrorType.Conflict, StatusCode.FailedPrecondition)]
    [InlineData(ErrorType.Unauthorized, StatusCode.Unauthenticated)]
    [InlineData(ErrorType.Unexpected, StatusCode.Internal)]
    public void Maps_error_type_to_grpc_status(ErrorType type, StatusCode expected)
        => Assert.Equal(expected, ErrorTransport.ToGrpcStatusCode(type));

    [Fact]
    public void ToProblemDetails_emits_rfc7807_with_status_code_and_error_code()
    {
        var error = Error.NotFound("goods_receipt.not_found", "GoodsReceipt tidak ditemukan.");

        var problem = Assert.IsType<ProblemHttpResult>(error.ToProblemDetails());

        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Equal("GoodsReceipt tidak ditemukan.", problem.ProblemDetails.Detail);
        Assert.Equal("goods_receipt.not_found", problem.ProblemDetails.Extensions["errorCode"]);
    }
}
