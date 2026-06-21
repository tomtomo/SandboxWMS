using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.BuildingBlocks.Web.Security;

// What: composition adapter ICurrentUser untuk host HTTP (ADR-0027)
// Why: host yang menerima request HTTP (mis. Inbound.Api) cukup AddHttpContextCurrentUser() —
// identitas mengalir dari JWT/HttpContext ke IAuditable & audit-log. Host origin-mesin murni
// (consumer/worker) TIDAK pakai ini; ia mendaftarkan SystemCurrentUser (intent berbeda eksplisit).
public static class CurrentUserExtensions
{
    public static IServiceCollection AddHttpContextCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        return services;
    }
}
