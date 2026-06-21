using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate Product (DDD persistence; ADR-0010)
// Why: identity NATURAL = SKU (overview §D) → kolom PK "sku" (string), bukan surrogate Guid;
// strongly-typed ProductId dikonversi ke string. Field kritikal (uom/batch_tracking_required)
// dipersist apa adanya — sumber snapshot core (ADR-0014). shelf_life_days nullable.
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new ProductId(value))
            .HasColumnName("sku")
            .HasMaxLength(64)
            .ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(256).IsRequired();
        builder.Property(p => p.Uom).HasMaxLength(32).IsRequired();
        builder.Property(p => p.BatchTrackingRequired).IsRequired();
        builder.Property(p => p.ExpiryTrackingRequired).IsRequired();
        builder.Property(p => p.QcRequiredOnReceipt).IsRequired();
        builder.Property(p => p.ShelfLifeDays);
        builder.Property(p => p.IsActive).IsRequired();
    }
}
