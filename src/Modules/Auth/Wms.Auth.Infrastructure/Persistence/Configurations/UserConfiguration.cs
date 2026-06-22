using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Auth.Domain;

namespace Wms.Auth.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate User (DDD persistence; ADR-0010)
// Why: username UNIQUE (login lookup) → unique index. Status enum→string (urutan enum tak mengikat
// persistence). PasswordHash OPAQUE (ADR-0016) — kolom string, domain tak interpretasi. Koleksi referensi
// (RoleIds/WarehouseIds) di-serialize JSON ke kolom text via value converter: deterministik & bebas dari
// ambiguitas EF8 primitive-collection vs Npgsql array — TAK di-query ke dalam (hanya round-trip mint).
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(user => user.Username).HasMaxLength(64).IsRequired();
        builder.HasIndex(user => user.Username).IsUnique();
        builder.Property(user => user.Email).HasMaxLength(256).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(user => user.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(user => user.FailedLoginCount).IsRequired();

        // What: computed accessor read-only (project Guid→RoleId) — TAK di-map; backing field di-map eksplisit.
        // Why: RoleIds bertipe IReadOnlyCollection<RoleId> akan didiskualifikasi EF sebagai navigation ke
        // entity RoleId — Ignore mencegahnya; round-trip lewat field primitif "_roleIds" (Guid).
        builder.Ignore(user => user.RoleIds);
        builder.Ignore(user => user.AssignedWarehouseIds);

        // What: koleksi referensi (List<Guid> backing) → JSON text via converter — deterministik, tak
        // di-query ke dalam (round-trip mint). ValueComparer agar change-tracking koleksi mutable benar.
        var guidListComparer = new ValueComparer<List<Guid>>(
            (left, right) => left!.SequenceEqual(right!),
            value => value.Aggregate(0, (hash, id) => HashCode.Combine(hash, id.GetHashCode())),
            value => value.ToList());

        builder.Property<List<Guid>>("_roleIds")
            .HasColumnName("role_ids")
            .HasConversion(
                ids => JsonSerializer.Serialize(ids, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<Guid>>(json, (JsonSerializerOptions?)null)!,
                guidListComparer);

        builder.Property<List<Guid>>("_assignedWarehouseIds")
            .HasColumnName("assigned_warehouse_ids")
            .HasConversion(
                ids => JsonSerializer.Serialize(ids, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<Guid>>(json, (JsonSerializerOptions?)null)!,
                guidListComparer);
    }
}
