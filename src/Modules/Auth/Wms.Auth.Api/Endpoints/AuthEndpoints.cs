using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.Auth.Application.Features.Login;
using Wms.Auth.Application.Features.Logout;
using Wms.Auth.Application.Features.Refresh;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;

namespace Wms.Auth.Api.Endpoints;

// What: REST endpoints autentikasi (ADR-0006 REST untuk UI/gateway) — login/refresh/logout
// Why: jalur kredensial via REST (UI/gateway), gRPC tetap untuk read service-to-service. Endpoint
// ANONYMOUS by design (login = cara memperoleh token; UseAuthentication hanya MENGISI principal, tak
// memblok tanpa [Authorize] — authZ deferred ADR-0012). Result→HTTP via ToProblemDetails (401 untuk
// InvalidCredentials, dari Error.Type otomatis ADR-0019). Refresh token MENTAH ada di body response
// (satu-satunya titik raw keluar, ADR-0016) — client menyimpannya.
public sealed class AuthEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new LoginCommand(request.Username, request.Password), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
        });

        group.MapPost("/refresh", async (RefreshRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new RefreshCommand(request.RefreshToken), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
        });

        group.MapPost("/logout", async (LogoutRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });

        // What: diagnostic identitas aktif — buktikan JWT bearer → ICurrentUser identitas NYATA (ADR-0016/0027)
        // Why: endpoint introspeksi standar auth-service; nilai UserId = `sub` token saat request terotentikasi,
        // "anonymous" saat tanpa token (CurrentUserResolver) — fondasi audit created_by yang nyata (ganti SYSTEM).
        group.MapGet("/me", (ICurrentUser currentUser) => Results.Ok(new { userId = currentUser.UserId }));
    }
}

public sealed record LoginRequest(string Username, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);
