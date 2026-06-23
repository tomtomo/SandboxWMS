using Grpc.Core;
using Polly.Registry;
using Wms.BuildingBlocks.Infrastructure.Resilience;
using Wms.Inventory.Application.Abstractions;
using Wms.MasterData.Grpc;

namespace Wms.Inventory.Infrastructure.MasterData;

// What: Adapter (ACL; Hexagonal) — ILocationCatalog via gRPC read-API MasterData (ADR-0006/0011)
// Why: realisasi port dengan client gRPC generated + RESILIENCE pipeline split-timeout (ADR-0020 —
// ResiliencePipelineProvider<string> key "wms-grpc": timeout 30s absorb cold-start + retry + CB).
// Anti-Corruption Layer: LocationKind Inventory → proto LocationType; LocationReply → kode string.
// RpcException NotFound → null (consumer gagalkan = DLQ: lokasi default tak terkonfigurasi).
internal sealed class GrpcLocationCatalog(
    MasterDataReadApi.MasterDataReadApiClient client,
    ResiliencePipelineProvider<string> pipelineProvider) : ILocationCatalog
{
    public async Task<string?> GetDefaultLocationCodeAsync(
        string warehouseId, LocationKind kind, CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineProvider.GetPipeline(ResiliencePipelineDefaults.GrpcPipelineKey);
        try
        {
            var reply = await pipeline.ExecuteAsync(
                async token => await client.GetDefaultLocationAsync(
                    new GetDefaultLocationRequest { WarehouseId = warehouseId, Type = ToProto(kind) },
                    cancellationToken: token),
                cancellationToken);
            return reply.Code;
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    // What: mapping LocationKind Inventory → proto LocationType (ACL translation) — fail-loud default
    // Why: LocationKind baru yang belum ter-map = crash early (ADR-0019), bukan diam jadi Unspecified
    // (yang read-API baca sebagai NotFound) → masking nilai valid-tapi-salah di ACL (co-symptom UF-05).
    private static LocationType ToProto(LocationKind kind) => kind switch
    {
        LocationKind.ReceivingArea => LocationType.ReceivingArea,
        LocationKind.QuarantineArea => LocationType.QuarantineArea,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "LocationKind tak ter-map ke proto"),
    };
}
