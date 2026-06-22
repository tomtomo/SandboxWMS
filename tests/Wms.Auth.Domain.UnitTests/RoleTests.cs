using Wms.Auth.Domain;

namespace Wms.Auth.Domain.UnitTests;

// What: unit test aggregate Role (RBAC grouping + soft-delete, overview §E / ADR-0012)
// Why: Role = kumpulan permission yang di-assign ke User; saat mint JWT, HANYA role aktif yang
// menyumbang permission ke claim (IsActive filter, ADR-0012). Factory + mutasi permission diuji agar
// claim-source konsisten; soft-delete isActive (ADR-0014) menggigit jalur mint.
public sealed class RoleTests
{
    private static Role CreateValid() => Role.Create("ADMIN", "Administrator").Value;

    [Fact]
    public void Create_succeeds_active_with_no_permissions()
    {
        var role = CreateValid();

        Assert.Equal("ADMIN", role.Code);
        Assert.Equal("Administrator", role.Name);
        Assert.True(role.IsActive);
        Assert.Empty(role.PermissionCodes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_code_blank(string code)
    {
        var result = Role.Create(code, "Administrator");

        Assert.True(result.IsFailure);
        Assert.Equal(RoleErrors.MissingCode.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_name_blank(string name)
    {
        var result = Role.Create("ADMIN", name);

        Assert.Equal(RoleErrors.MissingName.Code, result.Error.Code);
    }

    [Fact]
    public void AddPermission_adds_code()
    {
        var role = CreateValid();

        role.AddPermission("Inbound.PostGR");

        Assert.Contains("Inbound.PostGR", role.PermissionCodes);
    }

    [Fact]
    public void AddPermission_is_idempotent()
    {
        var role = CreateValid();

        role.AddPermission("Inbound.PostGR");
        role.AddPermission("Inbound.PostGR");

        Assert.Single(role.PermissionCodes);
    }

    [Fact]
    public void RemovePermission_removes_code()
    {
        var role = CreateValid();
        role.AddPermission("Inbound.PostGR");

        role.RemovePermission("Inbound.PostGR");

        Assert.Empty(role.PermissionCodes);
    }

    [Fact]
    public void Deactivate_then_activate_round_trips()
    {
        var role = CreateValid();

        Assert.True(role.Deactivate().IsSuccess);
        Assert.False(role.IsActive);
        Assert.True(role.Activate().IsSuccess);
        Assert.True(role.IsActive);
    }

    [Fact]
    public void Deactivate_fails_when_already_inactive()
    {
        var role = CreateValid();
        role.Deactivate();

        Assert.Equal(RoleErrors.AlreadyInactive.Code, role.Deactivate().Error.Code);
    }
}
