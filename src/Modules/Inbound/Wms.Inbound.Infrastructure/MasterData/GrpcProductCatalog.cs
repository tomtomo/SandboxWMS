using Grpc.Core;
using Polly.Registry;
using Wms.BuildingBlocks.Infrastructure.Resilience;
using Wms.Inbound.Application.Abstractions;
using Wms.MasterData.Grpc;

namespace Wms.Inbound.Infrastructure.MasterData;

// What: Adapter (ACL; Hexagonal) — IProductCatalog via gRPC read-API MasterData (ADR-0006/0011)
// Why: realisasi port dengan client gRPC generated + RESILIENCE pipeline split-timeout (ADR-0020 —
// ResiliencePipelineProvider<string> per-client key "wms-grpc": timeout 30s absorb cold-start + retry +
// circuit-breaker). Anti-Corruption Layer: ProductReply asing → ProductSnapshot Inbound. RpcException
// NotFound → null (product tak dikenal, no-throw friendly).
// How: bungkus call gRPC dengan pipeline yang di-resolve dari provider.
internal sealed class GrpcProductCatalog(
    MasterDataReadApi.MasterDataReadApiClient client,
    ResiliencePipelineProvider<string> pipelineProvider) : IProductCatalog
{
    public async Task<ProductSnapshot?> GetProductAsync(string sku, CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineProvider.GetPipeline(ResiliencePipelineDefaults.GrpcPipelineKey);
        try
        {
            var reply = await pipeline.ExecuteAsync(
                async token => await client.GetProductAsync(
                    new GetProductRequest { Sku = sku }, cancellationToken: token),
                cancellationToken);
            return new ProductSnapshot(reply.Uom);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }
}
