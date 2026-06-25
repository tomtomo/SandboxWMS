using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate OutboundOrder (DDD persistence; ADR-0010 / ADR-0014)
// Why: memetakan aggregate root + owned collection (orderLines) ke schema "outbound" tanpa atribut EF di
// domain (POCO, FF#2). Strongly-typed id → Guid; Status → string. orderLines di-NORMALIZE ke tabel terpisah
// tapi tetap SATU aggregate (invariant di domain). PropertyAccessMode.Field agar EF baca/tulis backing field.
public sealed class OutboundOrderConfiguration : IEntityTypeConfiguration<OutboundOrder>
{
    public void Configure(EntityTypeBuilder<OutboundOrder> builder)
    {
        builder.ToTable("outbound_orders");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, value => new OutboundOrderId(value))
            .ValueGeneratedNever();

        builder.Property(o => o.CustomerId).HasMaxLength(64).IsRequired();
        builder.Property(o => o.ShipTo).HasMaxLength(256).IsRequired();
        builder.Property(o => o.WaveId);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.OwnsMany(o => o.OrderLines, line =>
        {
            line.ToTable("order_lines");
            line.WithOwner().HasForeignKey("outbound_order_id");
            line.Property<int>("id");
            line.HasKey("id");
            line.Property(entry => entry.Sku).HasMaxLength(64).IsRequired();
            line.Property(entry => entry.Qty).IsRequired();
            line.Property(entry => entry.Uom).HasMaxLength(16).IsRequired();
            // ADR-0034: status alokasi line (string enum-name); default Pending utk row lama (migrasi)
            line.Property(entry => entry.AllocationStatus)
                .HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(OrderLineAllocationStatus.Pending);
        });

        builder.Navigation(o => o.OrderLines).UsePropertyAccessMode(PropertyAccessMode.Field);

        // back-reference query: order terikat wave (DispatchWave → Close)
        builder.HasIndex(o => o.WaveId);
    }
}
