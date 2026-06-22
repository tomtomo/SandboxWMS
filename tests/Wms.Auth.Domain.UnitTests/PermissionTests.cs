using Wms.Auth.Domain;

namespace Wms.Auth.Domain.UnitTests;

// What: unit test reference entity Permission (ADR-0026 reference data)
// Why: Permission = capability granular (`Module.Action`) yang di-SEED sebagai planning catalog
// (ADR-0012, authZ deferred) — bukan aggregate ber-state. Factory cukup menegakkan code non-empty
// (natural key) agar katalog konsisten; tak ada state machine / domain event.
public sealed class PermissionTests
{
    [Fact]
    public void Create_succeeds_and_captures_fields()
    {
        var result = Permission.Create("Inbound.PostGR", "Posting Goods Receipt.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Inbound.PostGR", result.Value.Code);
        Assert.Equal("Posting Goods Receipt.", result.Value.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_code_blank(string code)
    {
        var result = Permission.Create(code, "desc");

        Assert.True(result.IsFailure);
        Assert.Equal(PermissionErrors.MissingCode.Code, result.Error.Code);
    }
}
