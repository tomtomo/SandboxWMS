using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate GRAttachment (DDD persistence; ADR-0015)
// Why: aggregate TERPISAH dgn tabel sendiri (gr_attachments); tertaut ke GoodsReceipt via logical FK
// goodsReceiptId yang DI-INDEX, TANPA navigation property (tak menyeret load GoodsReceipt). Row hanya
// metadata + blobPath — byte di object storage.
// How: HasConversion untuk strongly-typed id; HasIndex(GoodsReceiptId) tanpa HasOne/WithMany (logical FK).
public sealed class GRAttachmentConfiguration : IEntityTypeConfiguration<GRAttachment>
{
    public void Configure(EntityTypeBuilder<GRAttachment> builder)
    {
        builder.ToTable("gr_attachments");

        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id)
            .HasConversion(id => id.Value, value => new GRAttachmentId(value))
            .ValueGeneratedNever();

        builder.Property(attachment => attachment.GoodsReceiptId).IsRequired();
        builder.HasIndex(attachment => attachment.GoodsReceiptId);

        builder.Property(attachment => attachment.FileName).HasMaxLength(256).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(attachment => attachment.SizeBytes).IsRequired();
        builder.Property(attachment => attachment.BlobPath).HasMaxLength(1024).IsRequired();
        builder.Property(attachment => attachment.UploadedAt).IsRequired();
    }
}
