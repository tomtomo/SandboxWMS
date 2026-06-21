using System.Security.Claims;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.BuildingBlocks.UnitTests.Security;

// What: unit test invariant SYSTEM-actor convention (DoD Phase 02c; ADR-0027)
// Why: menegakkan keputusan keamanan inti — SYSTEM di-key pada KETIADAAN request context,
// BUKAN pada !IsAuthenticated. Test anon≠SYSTEM adalah penjaga utama: kalau resolver salah
// memetakan anonymous HTTP ke SYSTEM, terjadi privilege-leak lintas-warehouse (opsi B ADR-0027).
// Resolver murni (nol transport) → bisa diuji tanpa web host.
public sealed class CurrentUserResolverTests
{
    [Fact]
    public void No_request_context_resolves_system()
    {
        // origin mesin (consumer bus / job / seeder / s2s): tak ada HttpContext → SYSTEM
        var actor = CurrentUserResolver.Resolve(hasRequestContext: false, principal: null);

        Assert.Equal(SystemActor.Id, actor);
    }

    [Fact]
    public void Anonymous_http_request_does_not_resolve_system()
    {
        // INVARIANT: ada request context tapi tak terotentikasi → anonymous, TAK BOLEH SYSTEM
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var actor = CurrentUserResolver.Resolve(hasRequestContext: true, principal: anonymous);

        Assert.NotEqual(SystemActor.Id, actor);
        Assert.Equal(SystemActor.Anonymous, actor);
    }

    [Fact]
    public void Authenticated_http_request_resolves_user_id()
    {
        var authenticated = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")], authenticationType: "Test"));

        var actor = CurrentUserResolver.Resolve(hasRequestContext: true, principal: authenticated);

        Assert.Equal("user-123", actor);
    }

    [Fact]
    public void Authenticated_without_id_claim_falls_back_to_anonymous_not_system()
    {
        // terotentikasi tapi tanpa klaim identitas → tetap BUKAN SYSTEM (fail-safe ke anonymous)
        var authenticated = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "Test"));

        var actor = CurrentUserResolver.Resolve(hasRequestContext: true, principal: authenticated);

        Assert.NotEqual(SystemActor.Id, actor);
        Assert.Equal(SystemActor.Anonymous, actor);
    }
}
