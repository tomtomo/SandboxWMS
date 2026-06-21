using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.DependencyInjection;
using Wms.Inventory.Application.Features.CompletePutaway;

namespace Wms.Inventory.Application.DependencyInjection;

// What: composition modul Inventory (Application) — CQRS pipeline wiring (ADR-0004)
// Why: host cukup AddInventoryApplication() untuk slice REST (CompletePutaway). Mendaftarkan MediatR
// (scan handler slice Inventory) + urutan pipeline behavior BuildingBlocks (Logging→Authz→Validation→
// AuditLog→Transaction) + validator FluentValidation — identik dengan modul Inbound. CATATAN: consumer
// integration-event (GRConfirmed/WaveReleased/…) BUKAN MediatR handler — didaftarkan di Infrastructure
// (AddInventoryInfrastructure), lewat dispatcher, bukan pipeline command ini.
// How: AddMediatR scan assembly Application ini; AddBuildingBlocksBehaviors menyuntik behavior berurutan;
// AddValidatorsFromAssembly menemukan IValidator slice.
public static class InventoryApplicationExtensions
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<CompletePutawayCommand>();
            configuration.AddBuildingBlocksBehaviors();
        });

        services.AddValidatorsFromAssemblyContaining<CompletePutawayValidator>();
        return services;
    }
}
