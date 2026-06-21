namespace Wms.BuildingBlocks.Application.Security;

// What: Null Object (GoF) — ICurrentUser untuk host origin-mesin (ADR-0027)
// Why: host tanpa transport HTTP sama sekali (worker murni / seeder / MigrationRunner) tetap
// butuh principal untuk IAuditable & audit-log. Daripada memaksa IHttpContextAccessor yang
// HttpContext-nya selalu null, host begini mendaftarkan adapter eksplisit ini → intent jelas
// ("ini memang mesin") dan nol dependensi web. Setara CurrentUserResolver.Resolve(false, null).
// How: konstanta SystemActor.Id; tak ada state, tak ada request context.
public sealed class SystemCurrentUser : ICurrentUser
{
    public string UserId => SystemActor.Id;
}
