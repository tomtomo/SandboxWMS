using Microsoft.AspNetCore.Routing;
using Wms.Auth.Api.Endpoints;

namespace Wms.Auth.Api;

// What: composition endpoint REST modul Auth (ADR-0006)
// Why: host cukup app.MapAuthEndpoints() — login/refresh/logout terdaftar di satu tempat. gRPC read-API
// service (AuthReadService) di-map terpisah oleh host (app.MapGrpcService<AuthReadService>()) karena
// butuh runtime Grpc.AspNetCore.
public static class AuthApiExtensions
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        new AuthEndpoints().MapEndpoint(app);
        return app;
    }
}
