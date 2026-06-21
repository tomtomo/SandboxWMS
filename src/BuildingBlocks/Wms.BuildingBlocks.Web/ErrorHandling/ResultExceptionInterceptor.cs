using Grpc.Core;
using Grpc.Core.Interceptors;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Web.ErrorHandling;

// What: exception pembawa Error melintasi batas gRPC (ADR-0019)
// Why: handler mengembalikan Result (no-throw-for-business); di adapter gRPC, Result.Failure
// di-angkat jadi exception ini supaya SATU interceptor memetakannya ke RpcException — service
// method gRPC tak menyusun Status sendiri (mapping tak tersebar).
public sealed class ResultFailureException(Error error) : Exception(error.Message)
{
    public Error Error { get; } = error;
}

// What: gRPC Server Interceptor — Error → RpcException (ADR-0019; ADR-0006)
// Why: kegagalan bisnis dipetakan ke StatusCode gRPC yang semantiknya SAMA dengan REST,
// lewat ErrorTransport (satu tabel) — bukan Unknown/Internal generik. Error.Code dibawa
// sebagai trailer supaya client bisa membaca kode stabil.
// How: bungkus continuation unary; tangkap ResultFailureException → RpcException(Status +
// trailer). Belum di-mount (belum ada modul *.Grpc) — template siap saat service gRPC lahir.
public sealed class ResultExceptionInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (ResultFailureException exception)
        {
            var error = exception.Error;
            var trailers = new Metadata { { "error-code", error.Code } };
            throw new RpcException(
                new Status(ErrorTransport.ToGrpcStatusCode(error.Type), error.Message), trailers);
        }
    }
}
