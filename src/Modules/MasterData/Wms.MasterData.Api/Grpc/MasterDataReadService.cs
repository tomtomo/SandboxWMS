using Grpc.Core;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Application.ReadModels;
using Wms.MasterData.Domain;
using Wms.MasterData.Grpc;
using DomainLocationType = Wms.MasterData.Domain.LocationType;
using ProtoLocationType = Wms.MasterData.Grpc.LocationType;

namespace Wms.MasterData.Api.Grpc;

// What: gRPC Service impl read-API MasterData (ADR-0006/0011) — reader-delegation
// Why: implement service base hasil codegen (*.Grpc) dengan DELEGASI ke IMasterDataReader (cache-aside
// decorator) — TIDAK inject DbContext (dijaga FF#8: gRPC `.Api` bebas EF, boundary read terisolasi).
// Not-found → ResultFailureException(NotFound) yang dipetakan ResultExceptionInterceptor (server) ke
// RpcException(StatusCode.NotFound) — service method tak menyusun Status sendiri (mapping tak tersebar,
// ADR-0019). Mapping LocationType domain→proto EKSPLISIT (bukan ordinal — urutan enum bisa berbeda).
public sealed class MasterDataReadService(IMasterDataReader reader) : MasterDataReadApi.MasterDataReadApiBase
{
    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        var product = await reader.GetProductAsync(request.Sku, context.CancellationToken);
        if (product is null)
            throw new ResultFailureException(ProductErrors.NotFound);

        return ToReply(product);
    }

    public override async Task<WarehouseReply> GetWarehouse(GetWarehouseRequest request, ServerCallContext context)
    {
        // malformed id → Guid.Empty → reader null → NotFound (read-API tak bocorkan detail parsing)
        Guid.TryParse(request.WarehouseId, out var id);
        var warehouse = await reader.GetWarehouseAsync(id, context.CancellationToken);
        if (warehouse is null)
            throw new ResultFailureException(WarehouseErrors.NotFound);

        return new WarehouseReply
        {
            WarehouseId = warehouse.WarehouseId.ToString(),
            Name = warehouse.Name,
            Address = warehouse.Address,
        };
    }

    public override async Task<LocationReply> GetLocation(GetLocationRequest request, ServerCallContext context)
    {
        Guid.TryParse(request.LocationId, out var id);
        var location = await reader.GetLocationAsync(id, context.CancellationToken);
        if (location is null)
            throw new ResultFailureException(LocationErrors.NotFound);

        return new LocationReply
        {
            LocationId = location.LocationId.ToString(),
            WarehouseId = location.WarehouseId.ToString(),
            Type = ToProto(location.Type),
            Code = location.Code,
        };
    }

    public override async Task<LocationReply> GetDefaultLocation(GetDefaultLocationRequest request, ServerCallContext context)
    {
        // proto type Unspecified/unknown → null → NotFound; warehouse malformed → Guid.Empty → null → NotFound
        var domainType = FromProto(request.Type);
        Guid.TryParse(request.WarehouseId, out var warehouseId);
        var location = domainType is null
            ? null
            : await reader.GetDefaultLocationAsync(warehouseId, domainType.Value, context.CancellationToken);
        if (location is null)
            throw new ResultFailureException(LocationErrors.NotFound);

        return new LocationReply
        {
            LocationId = location.LocationId.ToString(),
            WarehouseId = location.WarehouseId.ToString(),
            Type = ToProto(location.Type),
            Code = location.Code,
        };
    }

    private static ProductReply ToReply(ProductReadModel product)
    {
        var reply = new ProductReply
        {
            Sku = product.Sku,
            Name = product.Name,
            Uom = product.Uom,
            BatchTrackingRequired = product.BatchTrackingRequired,
            ExpiryTrackingRequired = product.ExpiryTrackingRequired,
            QcRequiredOnReceipt = product.QcRequiredOnReceipt,
        };
        // proto3 optional: hanya set bila ada (null = field absen di wire)
        if (product.ShelfLifeDays is { } days)
            reply.ShelfLifeDays = days;
        return reply;
    }

    // What: mapping eksplisit domain LocationType → proto LocationType (BUKAN ordinal)
    private static ProtoLocationType ToProto(DomainLocationType type) => type switch
    {
        DomainLocationType.ReceivingArea => ProtoLocationType.ReceivingArea,
        DomainLocationType.Rack => ProtoLocationType.Rack,
        DomainLocationType.QuarantineArea => ProtoLocationType.QuarantineArea,
        DomainLocationType.StagingArea => ProtoLocationType.StagingArea,
        _ => ProtoLocationType.Unspecified,
    };

    // What: mapping eksplisit proto LocationType → domain (null untuk Unspecified/unknown → NotFound)
    private static DomainLocationType? FromProto(ProtoLocationType type) => type switch
    {
        ProtoLocationType.ReceivingArea => DomainLocationType.ReceivingArea,
        ProtoLocationType.Rack => DomainLocationType.Rack,
        ProtoLocationType.QuarantineArea => DomainLocationType.QuarantineArea,
        ProtoLocationType.StagingArea => DomainLocationType.StagingArea,
        _ => null,
    };
}
