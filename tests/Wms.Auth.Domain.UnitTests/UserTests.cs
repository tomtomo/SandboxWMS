using Wms.Auth.Domain;

namespace Wms.Auth.Domain.UnitTests;

// What: unit test aggregate User (identity lifecycle + lockout, overview §E)
// Why: User = identitas yang bisa login; status Active/Locked/Disabled menggigit jalur mint token
// (hanya Active boleh terbit JWT, ADR-0012). Lockout (failedLoginCount>threshold) & rehash-on-upgrade
// (ChangePasswordHash) adalah invariant keamanan login → diuji eksplisit. PasswordHash OPAQUE (domain
// tak tahu algoritma, ADR-0016) — hanya disimpan & diganti, tak diinterpretasi.
public sealed class UserTests
{
    private static User CreateValid() =>
        User.Create("alice", "alice@wms.local", "argon2id.hash").Value;

    [Fact]
    public void Create_succeeds_active_with_fields()
    {
        var user = CreateValid();

        Assert.Equal("alice", user.Username);
        Assert.Equal("alice@wms.local", user.Email);
        Assert.Equal("argon2id.hash", user.PasswordHash);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.True(user.CanAuthenticate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_fails_when_username_blank(string username)
    {
        var result = User.Create(username, "alice@wms.local", "hash");

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.MissingUsername.Code, result.Error.Code);
    }

    [Fact]
    public void Create_fails_when_email_blank()
    {
        var result = User.Create("alice", "  ", "hash");

        Assert.Equal(UserErrors.MissingEmail.Code, result.Error.Code);
    }

    [Fact]
    public void Create_fails_when_password_hash_blank()
    {
        var result = User.Create("alice", "alice@wms.local", "");

        Assert.Equal(UserErrors.MissingPasswordHash.Code, result.Error.Code);
    }

    [Fact]
    public void RecordFailedLogin_increments_count()
    {
        var user = CreateValid();

        user.RecordFailedLogin(lockThreshold: 5);

        Assert.Equal(1, user.FailedLoginCount);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void RecordFailedLogin_locks_when_threshold_reached()
    {
        var user = CreateValid();

        user.RecordFailedLogin(lockThreshold: 3);
        user.RecordFailedLogin(lockThreshold: 3);
        user.RecordFailedLogin(lockThreshold: 3);

        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.False(user.CanAuthenticate);
    }

    [Fact]
    public void RecordSuccessfulLogin_resets_count()
    {
        var user = CreateValid();
        user.RecordFailedLogin(lockThreshold: 5);

        var result = user.RecordSuccessfulLogin();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void RecordSuccessfulLogin_fails_when_not_active()
    {
        var user = CreateValid();
        user.Lock();

        var result = user.RecordSuccessfulLogin();

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.NotActive.Code, result.Error.Code);
    }

    [Fact]
    public void Unlock_restores_active_and_resets_count()
    {
        var user = CreateValid();
        user.RecordFailedLogin(lockThreshold: 1);
        Assert.Equal(UserStatus.Locked, user.Status);

        var result = user.Unlock();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void Disable_then_enable_round_trips()
    {
        var user = CreateValid();

        Assert.True(user.Disable().IsSuccess);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.False(user.CanAuthenticate);

        Assert.True(user.Enable().IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void ChangePasswordHash_updates_hash()
    {
        var user = CreateValid();

        user.ChangePasswordHash("argon2id.upgraded");

        Assert.Equal("argon2id.upgraded", user.PasswordHash);
    }

    [Fact]
    public void AssignRole_adds_and_is_idempotent()
    {
        var user = CreateValid();
        var role = RoleId.New();

        user.AssignRole(role);
        user.AssignRole(role);

        Assert.Single(user.RoleIds);
        Assert.Contains(role, user.RoleIds);
    }

    [Fact]
    public void AssignWarehouse_adds_and_is_idempotent()
    {
        var user = CreateValid();
        var warehouse = Guid.NewGuid();

        user.AssignWarehouse(warehouse);
        user.AssignWarehouse(warehouse);

        Assert.Single(user.AssignedWarehouseIds);
        Assert.Contains(warehouse, user.AssignedWarehouseIds);
    }
}
