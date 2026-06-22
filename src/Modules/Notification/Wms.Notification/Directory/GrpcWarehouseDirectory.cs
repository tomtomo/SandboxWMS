using Grpc.Core;
using Polly.Registry;
using Wms.BuildingBlocks.Infrastructure.Resilience;
using Wms.MasterData.Grpc;

namespace Wms.Notification.Directory;

// What: Adapter (ACL; Hexagonal) — IWarehouseDirectory via gRPC read-API MasterData (ADR-0006/0011)
// Why: realisasi port dgn client gRPC generated + RESILIENCE pipeline split-timeout (ADR-0020). ACL:
// WarehouseReply asing → WarehouseContext Notification. RpcException NotFound → null (warehouse tak
// dikenal, no-throw friendly → worker tetap dispatch tanpa enrichment nama).
// How: bungkus call gRPC dgn pipeline; token s2s + correlation via interceptor host (ADR-0021).
internal sealed class GrpcWarehouseDirectory(
    MasterDataReadApi.MasterDataReadApiClient client,
    ResiliencePipelineProvider<string> pipelineProvider) : IWarehouseDirectory
{
    public async Task<WarehouseContext?> GetWarehouseAsync(
        string warehouseId, CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineProvider.GetPipeline(ResiliencePipelineDefaults.GrpcPipelineKey);
        try
        {
            var reply = await pipeline.ExecuteAsync(
                async token => await client.GetWarehouseAsync(
                    new GetWarehouseRequest { WarehouseId = warehouseId }, cancellationToken: token),
                cancellationToken);
            return new WarehouseContext(reply.WarehouseId, reply.Name);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }
}
