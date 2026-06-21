using Grpc.Core;
using Polly.Registry;
using Wms.BuildingBlocks.Infrastructure.Resilience;
using Wms.MasterData.Grpc;
using Wms.Outbound.Application.Abstractions;

namespace Wms.Outbound.Infrastructure.MasterData;

// What: Adapter (ACL; Hexagonal) — IProductCatalog via gRPC read-API MasterData (ADR-0006/0011)
// Why: realisasi port dengan client gRPC generated + RESILIENCE pipeline split-timeout (ADR-0020 —
// dikonsumsi via ResiliencePipelineProvider<string> per-client key "wms-grpc": timeout 30s absorb
// cold-start scale-to-zero + retry + circuit-breaker). Anti-Corruption Layer: ProductReply asing →
// ProductSnapshot model Outbound. RpcException NotFound → null (product tak dikenal, no-throw friendly).
// How: bungkus call gRPC dengan pipeline yang di-resolve dari provider. CATATAN: HttpClient gRPC juga
// menerima standard resilience default (ServiceDefaults); split-timeout 30s eksplisit di pipeline ini —
// kalibrasi & opt-out HTTP-default untuk gRPC = Phase 07c (cold-start tak ter-exercise di Local).
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
