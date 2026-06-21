using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate GoodsReceipt (DDD persistence; ADR-0010 / ADR-0013)
// Why: memetakan aggregate root + tiga owned collection (expected/scanned/discrepancies) ke schema
// "inbound" — struktur konseptual di-NORMALIZE ke tabel terpisah, tapi tetap SATU aggregate di domain
// (invariant ditegakkan di domain, bukan DB). Tanpa mencemari domain dgn atribut EF (POCO murni, FF#2).
// How: HasConversion untuk strongly-typed id + enum→string; OwnsMany untuk tiap collection (shadow
// FK+PK); PropertyAccessMode.Field agar EF baca/tulis backing field, bukan property read-only;
// QuantityChecks di-Ignore (turunan transient — tak dipersist, ADR-0013).
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
        builder.Property(gr => gr.PoRef).HasMaxLength(64);
        builder.Property(gr => gr.SupplierId).HasMaxLength(64);
        builder.Property(gr => gr.DockDoor).HasMaxLength(64);
        builder.Property(gr => gr.HoldReason).HasMaxLength(512);
        builder.Property(gr => gr.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Ignore(gr => gr.QuantityChecks);

        builder.OwnsMany(gr => gr.ExpectedLines, line =>
        {
            line.ToTable("gr_expected_lines");
            line.WithOwner().HasForeignKey("goods_receipt_id");
            line.Property<int>("id");
            line.HasKey("id");
            line.Property(entry => entry.Sku).HasMaxLength(64).IsRequired();
            line.Property(entry => entry.ExpectedQty).IsRequired();
            line.Property(entry => entry.Uom).HasMaxLength(16).IsRequired();
        });

        builder.OwnsMany(gr => gr.ScannedLines, line =>
        {
            line.ToTable("gr_scanned_lines");
            line.WithOwner().HasForeignKey("goods_receipt_id");
            line.Property<int>("id");
            line.HasKey("id");
            line.Property(entry => entry.Sku).HasMaxLength(64).IsRequired();
            line.Property(entry => entry.ActualQty).IsRequired();
            line.Property(entry => entry.Batch).HasMaxLength(64);
            line.Property(entry => entry.Expiry);
            line.Property(entry => entry.LineStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        });

        builder.OwnsMany(gr => gr.Discrepancies, discrepancy =>
        {
            discrepancy.ToTable("gr_discrepancies");
            discrepancy.WithOwner().HasForeignKey("goods_receipt_id");
            discrepancy.Property<int>("id");
            discrepancy.HasKey("id");
            discrepancy.Property(entry => entry.Sku).HasMaxLength(64).IsRequired();
            discrepancy.Property(entry => entry.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
            discrepancy.Property(entry => entry.Action).HasConversion<string>().HasMaxLength(20);
            discrepancy.Property(entry => entry.Note).HasMaxLength(512);
            discrepancy.Ignore(entry => entry.IsResolved);
        });

        builder.Navigation(gr => gr.ExpectedLines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(gr => gr.ScannedLines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(gr => gr.Discrepancies).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
