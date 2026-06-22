using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Reporting.Projections;

namespace Wms.Reporting.Persistence.Configurations;

// What: EF mapping projection ReceivingSummary (denormalized read-model; ADR-0017)
// Why: PK komposit (supplier, hari) = bucket agregasi. SupplierId NON-NULL ("" tak diketahui).
public sealed class ReceivingSummaryConfiguration : IEntityTypeConfiguration<ReceivingSummary>
{
    public void Configure(EntityTypeBuilder<ReceivingSummary> builder)
    {
        builder.ToTable("receiving_summary");
        builder.HasKey(x => new { x.SupplierId, x.Day });
        builder.Property(x => x.SupplierId).HasMaxLength(128);
    }
}
