using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.DependencyInjection;
using Wms.MasterData.Application.Features.CreateProduct;

namespace Wms.MasterData.Application.DependencyInjection;

// What: composition modul MasterData (Application) — CQRS pipeline wiring (ADR-0004)
// Why: host cukup AddMasterDataApplication() untuk CRUD slices REST (Create/Deactivate Product/
// Warehouse/Location). Mendaftarkan MediatR (scan handler slice MasterData) + urutan pipeline behavior
// BuildingBlocks (Logging→Authz→Validation→AuditLog→Transaction) + validator FluentValidation —
// identik modul lain. Sisi READ (gRPC read-API) BUKAN lewat MediatR: IMasterDataReader (cache-aside
// decorator) di-wire di Infrastructure → di-inject langsung ke gRPC service (CQRS read bypass).
public static class MasterDataApplicationExtensions
{
    public static IServiceCollection AddMasterDataApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<CreateProductCommand>();
            configuration.AddBuildingBlocksBehaviors();
        });

        services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();
        return services;
    }
}
