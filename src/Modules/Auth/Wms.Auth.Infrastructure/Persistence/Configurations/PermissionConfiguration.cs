using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Auth.Domain;

namespace Wms.Auth.Infrastructure.Persistence.Configurations;

// What: EF Core mapping reference entity Permission (DDD; ADR-0026)
// Why: identity NATURAL = code (`Module.Action`) → kolom PK "code" (string), bukan surrogate. Reference
// data di-seed (planning catalog, ADR-0012) — tanpa state, hanya code + description.
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Id)
            .HasColumnName("code")
            .HasMaxLength(128)
            .ValueGeneratedNever();

        builder.Property(permission => permission.Description).HasMaxLength(256).IsRequired();
    }
}
