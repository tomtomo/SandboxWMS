using System.Security.Claims;

namespace Wms.BuildingBlocks.Application.Security;

// What: keputusan SYSTEM-actor murni (ADR-0027) — jantung konvensi, transport-free
// Why: aturan "SYSTEM saat tak ada request context" HARUS di-key pada KEHADIRAN request
// context, BUKAN pada !IsAuthenticated — kalau di-key pada !IsAuthenticated maka anonymous
// HTTP jadi SYSTEM (privilege-leak lintas-warehouse, opsi B yang ditolak ADR-0027). Logika
// ini ditaruh di Application sebagai fungsi MURNI supaya invariant anon≠SYSTEM bisa diuji
// unit tanpa menyalakan web host (ClaimsPrincipal = BCL System.Security.Claims, BUKAN ASP.NET
// — Application tetap nol-transport). Adapter HttpContext (Web) hanya MEN-FEED dua input ini.
// How: hasRequestContext=false → SYSTEM (mesin); ada context tapi principal tak terotentikasi
// → anonymous (invariant!); else → klaim identitas (NameIdentifier/sub), fallback anonymous.
public static class CurrentUserResolver
{
    public static string Resolve(bool hasRequestContext, ClaimsPrincipal? principal)
    {
        if (!hasRequestContext)
            return SystemActor.Id;

        if (principal?.Identity?.IsAuthenticated != true)
            return SystemActor.Anonymous;

        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? SystemActor.Anonymous;
    }
}
