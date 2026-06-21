using Microsoft.AspNetCore.Http;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.BuildingBlocks.Web.Security;

// What: Adapter (Hexagonal) — ICurrentUser dari HttpContext (ADR-0027)
// Why: port ICurrentUser sengaja nol-transport (Application); INILAH satu-satunya tempat
// HttpContext disentuh. Adapter tipis ini men-FEED dua fakta transport (ada-tidaknya request
// context + ClaimsPrincipal) ke keputusan murni CurrentUserResolver — menjaga logika SYSTEM-vs-
// anonymous tetap di Application & teruji-unit, sementara ketergantungan ASP.NET terkurung di Web.
// How: IHttpContextAccessor (AsyncLocal) → HttpContext null (mis. consumer/job di luar request)
// memetakan ke SYSTEM; ada context tapi anonymous → anonymous (invariant ADR-0027), bukan SYSTEM.
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserId => CurrentUserResolver.Resolve(
        hasRequestContext: accessor.HttpContext is not null,
        principal: accessor.HttpContext?.User);
}
