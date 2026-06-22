using Wms.Auth.Domain;

namespace Wms.Auth.Domain.UnitTests;

// What: unit test aggregate RefreshToken (rotation chain + replay defense, ADR-0016)
// Why: RefreshToken = jantung re-issue access JWT tanpa login ulang; invariant load-bearing —
// IsActive dihitung (revokedAt==null && now<expiresAt), Rotate hanya pada token aktif, Revoke
// idempotent untuk cascade. Status dihitung (bukan enum) → uji eksplisit batas waktu & revocation.
public sealed class RefreshTokenTests
{
    private static readonly UserId User = UserId.New();
    private static readonly DateTimeOffset Issued = new(2026, 6, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Expiry = Issued.AddDays(7);

    private static RefreshToken IssueValid() =>
        RefreshToken.Issue(RefreshTokenId.New(), User, "hash-abc", Issued, Expiry).Value;

    [Fact]
    public void Issue_succeeds_and_captures_fields()
    {
        var token = IssueValid();

        Assert.Equal(User, token.UserId);
        Assert.Equal("hash-abc", token.TokenHash);
        Assert.Equal(Issued, token.IssuedAt);
        Assert.Equal(Expiry, token.ExpiresAt);
        Assert.Null(token.RevokedAt);
        Assert.Null(token.ReplacedByTokenId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Issue_fails_when_token_hash_blank(string hash)
    {
        var result = RefreshToken.Issue(RefreshTokenId.New(), User, hash, Issued, Expiry);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.MissingTokenHash.Code, result.Error.Code);
    }

    [Fact]
    public void Issue_fails_when_expiry_not_after_issued()
    {
        var result = RefreshToken.Issue(RefreshTokenId.New(), User, "hash-abc", Issued, Issued);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.InvalidExpiry.Code, result.Error.Code);
    }

    [Fact]
    public void IsActive_true_when_not_revoked_and_not_expired()
    {
        var token = IssueValid();

        Assert.True(token.IsActive(Issued.AddDays(1)));
    }

    [Fact]
    public void IsActive_false_when_expired()
    {
        var token = IssueValid();

        Assert.False(token.IsActive(Expiry.AddSeconds(1)));
    }

    [Fact]
    public void IsActive_false_when_revoked()
    {
        var token = IssueValid();
        token.Revoke(Issued.AddDays(1));

        Assert.False(token.IsActive(Issued.AddDays(2)));
    }

    [Fact]
    public void Revoke_sets_revoked_at()
    {
        var token = IssueValid();
        var when = Issued.AddDays(1);

        token.Revoke(when);

        Assert.Equal(when, token.RevokedAt);
    }

    [Fact]
    public void Revoke_is_idempotent_keeping_first_timestamp()
    {
        var token = IssueValid();
        var first = Issued.AddDays(1);

        token.Revoke(first);
        token.Revoke(Issued.AddDays(2));

        Assert.Equal(first, token.RevokedAt);
    }

    [Fact]
    public void Rotate_marks_replaced_and_revoked()
    {
        var token = IssueValid();
        var replacement = RefreshTokenId.New();
        var when = Issued.AddDays(1);

        var result = token.Rotate(replacement, when);

        Assert.True(result.IsSuccess);
        Assert.Equal(replacement, token.ReplacedByTokenId);
        Assert.Equal(when, token.RevokedAt);
        Assert.False(token.IsActive(when));
    }

    [Fact]
    public void Rotate_fails_when_not_active()
    {
        var token = IssueValid();
        token.Revoke(Issued.AddDays(1));

        var result = token.Rotate(RefreshTokenId.New(), Issued.AddDays(2));

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.NotActive.Code, result.Error.Code);
    }
}
