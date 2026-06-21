using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.DependencyInjection;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;

namespace Wms.Inbound.Application.DependencyInjection;

// What: composition modul Inbound (Application) — CQRS pipeline wiring (ADR-0004)
// Why: host cukup AddInboundApplication(). Mendaftarkan MediatR (scan handler slice Inbound) +
// urutan pipeline behavior BuildingBlocks (Logging→Authz→Validation→Transaction) + validator
// FluentValidation — semua use-case Inbound mengalir lewat pipeline yang sama.
// How: AddMediatR scan assembly Application ini; AddBuildingBlocksBehaviors menyuntik 4 behavior
// berurutan; AddValidatorsFromAssembly menemukan IValidator slice.
public static class InboundApplicationExtensions
{
    public static IServiceCollection AddInboundApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<CreateGoodsReceiptCommand>();
            configuration.AddBuildingBlocksBehaviors();
        });

        services.AddValidatorsFromAssemblyContaining<CreateGoodsReceiptValidator>();
        return services;
    }
}
