using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Reporting.Projections;

namespace Wms.Reporting.Persistence.Configurations;

// What: EF mapping projection StockOnHandView (denormalized read-model; ADR-0017)
// Why: PK natural komposit (warehouse, sku, batch) = find-or-create-by-PK consumer. Batch NON-NULL
// ("" no-batch) supaya layak jadi bagian PK. Schema "reporting" (default via DbContext).
public sealed class StockOnHandViewConfiguration : IEntityTypeConfiguration<StockOnHandView>
{
    public void Configure(EntityTypeBuilder<StockOnHandView> builder)
    {
        builder.ToTable("stock_on_hand_view");
        builder.HasKey(x => new { x.WarehouseId, x.Sku, x.Batch });
        builder.Property(x => x.WarehouseId).HasMaxLength(64);
        builder.Property(x => x.Sku).HasMaxLength(64);
        builder.Property(x => x.Batch).HasMaxLength(64);
    }
}
