using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate Stock (DDD persistence; ADR-0010)
// Why: memetakan Stock ke schema "inventory" tanpa atribut EF di domain (POCO, FF#2);
// strongly-typed id dikonversi ke Guid; Status disimpan sebagai string (readable). Field lifecycle
// 03b (location/batch/expiry + allocatedToWaveId/pickingTaskId) ditambah; index pendukung query
// FEFO (status+sku) & removal per-wave (allocated_to_wave_id).
public sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stocks");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new StockId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.WarehouseId).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Sku).HasMaxLength(64).IsRequired();
        builder.Property(s => s.LocationId).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Batch).HasMaxLength(64);
        builder.Property(s => s.Expiry);
        builder.Property(s => s.Quantity).IsRequired();
        builder.Property(s => s.SourceGoodsReceiptId).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(s => s.AllocatedToWaveId);
        builder.Property(s => s.PickingTaskId);

        // FEFO candidate lookup: stock Available per sku, urut expiry (di consumer)
        builder.HasIndex(s => new { s.Status, s.Sku });
        // removal per-wave: stock Picked terikat wave (ShipmentDispatched)
        builder.HasIndex(s => s.AllocatedToWaveId);
    }
}
