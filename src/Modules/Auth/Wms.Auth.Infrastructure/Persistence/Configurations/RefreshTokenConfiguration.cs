using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Auth.Domain;

namespace Wms.Auth.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate RefreshToken (DDD persistence; ADR-0016)
// Why: TokenHash UNIQUE + indexed — di-query BY HASH tiap refresh (token mentah tak pernah disimpan,
// ADR-0016). ReplacedByTokenId nullable (rotation chain). UserId = FK LOGICAL (Guid), tanpa navigation
// property lintas-aggregate (Vernon IDDD) — boundary konsistensi per-aggregate. Bukan auditable.
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id)
            .HasConversion(id => id.Value, value => new RefreshTokenId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(token => token.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasColumnName("user_id")
            .IsRequired();
        builder.HasIndex(token => token.UserId);

        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.Property(token => token.IssuedAt).IsRequired();
        builder.Property(token => token.ExpiresAt).IsRequired();
        builder.Property(token => token.RevokedAt);

        builder.Property(token => token.ReplacedByTokenId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : new RefreshTokenId(value.Value))
            .HasColumnName("replaced_by_token_id");
    }
}
