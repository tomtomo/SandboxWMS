using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.DependencyInjection;
using Wms.Outbound.Application.Features.CreateWave;

namespace Wms.Outbound.Application.DependencyInjection;

// What: composition modul Outbound (Application) — CQRS pipeline wiring (ADR-0004)
// Why: host cukup AddOutboundApplication() untuk slice REST (ReceiveOrder/CreateWave/CompletePicking/
// DispatchWave). Mendaftarkan MediatR (scan handler slice Outbound) + urutan pipeline behavior BuildingBlocks
// (Logging→Authz→Validation→AuditLog→Transaction) + validator FluentValidation — identik dgn Inbound/Inventory.
// CATATAN: consumer StockAllocated BUKAN MediatR handler — didaftarkan di Infrastructure (lewat dispatcher).
// How: AddMediatR scan assembly Application ini; AddBuildingBlocksBehaviors menyuntik behavior berurutan;
// AddValidatorsFromAssembly menemukan IValidator slice.
public static class OutboundApplicationExtensions
{
    public static IServiceCollection AddOutboundApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<CreateWaveCommand>();
            configuration.AddBuildingBlocksBehaviors();
        });

        services.AddValidatorsFromAssemblyContaining<CreateWaveValidator>();
        return services;
    }
}
