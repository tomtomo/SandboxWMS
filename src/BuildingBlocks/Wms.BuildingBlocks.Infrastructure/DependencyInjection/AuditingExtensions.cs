using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Infrastructure.Auditing;

namespace Wms.BuildingBlocks.Infrastructure.DependencyInjection;

// What: composition auto-audit IAuditable (ADR-0027) — interceptor + default principal
// Why: tiap modul infra cukup AddAuditableEntityInterceptor() lalu menyambungkan interceptor
// ke DbContext options. Default ICurrentUser=SystemCurrentUser dipasang TryAdd → infra
// SELF-SUFFICIENT untuk origin-mesin (consumer/job/MigrationRunner/integration-test): tak perlu
// tiap konsumen mendaftarkan principal. Host HTTP MENG-OVERRIDE default ini dengan
// HttpContextCurrentUser (registrasi belakangan menang) supaya request membawa identitas nyata.
public static class AuditingExtensions
{
    public static IServiceCollection AddAuditableEntityInterceptor(this IServiceCollection services)
    {
        services.AddScoped<AuditableEntityInterceptor>();
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        return services;
    }
}
