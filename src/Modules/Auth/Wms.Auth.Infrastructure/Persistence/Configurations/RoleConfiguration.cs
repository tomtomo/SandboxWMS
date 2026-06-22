using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Auth.Domain;

namespace Wms.Auth.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate Role (DDD persistence; ADR-0010)
// Why: code UNIQUE (RBAC lookup). PermissionCodes (List<string>) di-serialize JSON ke kolom text via
// converter — sumber permission claim saat mint (role aktif saja, ADR-0012). isActive soft-delete
// (ADR-0014) ditegakkan global query filter di DbContext.
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id)
            .HasConversion(id => id.Value, value => new RoleId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(role => role.Code).HasMaxLength(64).IsRequired();
        builder.HasIndex(role => role.Code).IsUnique();
        builder.Property(role => role.Name).HasMaxLength(128).IsRequired();
        builder.Property(role => role.IsActive).IsRequired();

        // computed accessor read-only — TAK di-map; backing field "_permissionCodes" di-map eksplisit
        builder.Ignore(role => role.PermissionCodes);

        // What: PermissionCodes (List<string>) → JSON text. Backing field "_permissionCodes".
        var comparer = new ValueComparer<List<string>>(
            (left, right) => left!.SequenceEqual(right!),
            value => value.Aggregate(0, (hash, code) => HashCode.Combine(hash, code.GetHashCode())),
            value => value.ToList());
        builder.Property<List<string>>("_permissionCodes")
            .HasColumnName("permission_codes")
            .HasConversion(
                codes => JsonSerializer.Serialize(codes, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null)!,
                comparer);
    }
}
