using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate GoodsReceipt (DDD persistence; ADR-0010)
// Why: memetakan aggregate root + owned collection lines ke schema "inbound" tanpa
// mencemari domain dengan atribut EF (POCO murni, FF#2). Strongly-typed id dikonversi
// ke Guid; Lines dipetakan via backing field _lines menjaga enkapsulasi aggregate.
// How: HasConversion untuk GoodsReceiptId; OwnsMany untuk gr_lines (shadow FK + PK);
// PropertyAccessMode.Field agar EF baca/tulis _lines, bukan property read-only.
public sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("goods_receipts");

        builder.HasKey(gr => gr.Id);
        builder.Property(gr => gr.Id)
            .HasConversion(id => id.Value, value => new GoodsReceiptId(value))
            .ValueGeneratedNever();

        builder.Property(gr => gr.WarehouseId).HasMaxLength(64).IsRequired();
        builder.Property(gr => gr.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.OwnsMany(gr => gr.Lines, line =>
        {
            line.ToTable("gr_lines");
            line.WithOwner().HasForeignKey("goods_receipt_id");
            line.Property<int>("id");
            line.HasKey("id");
            line.Property(l => l.Sku).HasMaxLength(64).IsRequired();
            line.Property(l => l.Quantity).IsRequired();
        });

        builder.Navigation(gr => gr.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
