using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wms.Auth.Application.Features.Login;
using Wms.Auth.Application.Security;
using Wms.BuildingBlocks.Application.DependencyInjection;

namespace Wms.Auth.Application.DependencyInjection;

// What: composition modul Auth (Application) — CQRS pipeline wiring (ADR-0004)
// Why: host cukup AddAuthApplication() untuk slice Login/Refresh/Logout. Mendaftarkan MediatR (scan
// handler slice Auth) + urutan pipeline behavior BuildingBlocks (Logging→Authz→Validation→AuditLog→
// Transaction) + validator FluentValidation + TokenMinter (application service). Sisi READ (gRPC read-API)
// BUKAN lewat MediatR: IAuthReader (cache-aside decorator) di-wire Infrastructure → inject ke gRPC service.
public static class AuthApplicationExtensions
{
    public static IServiceCollection AddAuthApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<LoginCommand>();
            configuration.AddBuildingBlocksBehaviors();
        });

        services.AddValidatorsFromAssemblyContaining<LoginValidator>();
        services.AddScoped<TokenMinter>();
        return services;
    }
}
