using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate Warehouse (DDD persistence; ADR-0010)
// Why: memetakan Warehouse ke schema "masterdata" tanpa atribut EF di domain (POCO, FF#2);
// strongly-typed id dikonversi ke Guid. Soft-delete query filter dipasang di DbContext.OnModelCreating
// (flag-gated, ADR-0014), bukan di sini.
public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => new WarehouseId(value))
            .ValueGeneratedNever();

        builder.Property(w => w.Name).HasMaxLength(256).IsRequired();
        builder.Property(w => w.Address).HasMaxLength(512).IsRequired();
        builder.Property(w => w.IsActive).IsRequired();
    }
}
