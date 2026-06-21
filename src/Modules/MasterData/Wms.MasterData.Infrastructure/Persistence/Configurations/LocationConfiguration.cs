using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate Location (DDD persistence; ADR-0010)
// Why: strongly-typed id & referensi WarehouseId dikonversi ke Guid (reference by-id, bukan FK
// navigation — boundary aggregate). Type=LocationType disimpan STRING (readable, urutan enum tak
// mengikat). Index (warehouse_id, type) mendukung lookup default-location by-type (dipakai kelak).
public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new LocationId(value))
            .ValueGeneratedNever();

        builder.Property(l => l.WarehouseId)
            .HasConversion(id => id.Value, value => new WarehouseId(value))
            .IsRequired();

        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(l => l.Code).HasMaxLength(64).IsRequired();
        builder.Property(l => l.IsActive).IsRequired();

        builder.HasIndex(l => new { l.WarehouseId, l.Type });
    }
}
