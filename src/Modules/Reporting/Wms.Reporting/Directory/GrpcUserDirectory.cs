using Grpc.Core;
using Polly.Registry;
using Wms.Auth.Grpc;
using Wms.BuildingBlocks.Infrastructure.Resilience;

namespace Wms.Reporting.Directory;

// What: Adapter (ACL; Hexagonal) — IUserDirectory via gRPC read-API Auth (ADR-0006/0011)
// Why: realisasi port dgn client gRPC generated + RESILIENCE pipeline split-timeout (ADR-0020 —
// ResiliencePipelineProvider<string> key "wms-grpc": timeout absorb cold-start + retry + CB). Anti-Corruption
// Layer: UserReply asing → username murni (subset yang Reporting butuh). RpcException NotFound → null.
// How: bungkus call gRPC dgn pipeline yang di-resolve dari provider; token s2s + correlation disuntik
// ServiceAuthCallCredentials interceptor (di-wire host ConfigureChannel, ADR-0021).
internal sealed class GrpcUserDirectory(
    AuthReadApi.AuthReadApiClient client,
    ResiliencePipelineProvider<string> pipelineProvider) : IUserDirectory
{
    public async Task<string?> GetUsernameAsync(string userId, CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineProvider.GetPipeline(ResiliencePipelineDefaults.GrpcPipelineKey);
        try
        {
            var reply = await pipeline.ExecuteAsync(
                async token => await client.GetUserAsync(
                    new GetUserRequest { UserId = userId }, cancellationToken: token),
                cancellationToken);
            return reply.Username;
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }
}
