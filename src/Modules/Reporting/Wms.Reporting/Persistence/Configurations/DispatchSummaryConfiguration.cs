using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Reporting.Projections;

namespace Wms.Reporting.Persistence.Configurations;

// What: EF mapping projection DispatchSummary (denormalized read-model; ADR-0017)
// Why: PK Day = bucket harian (berapa wave dispatched + volume).
public sealed class DispatchSummaryConfiguration : IEntityTypeConfiguration<DispatchSummary>
{
    public void Configure(EntityTypeBuilder<DispatchSummary> builder)
    {
        builder.ToTable("dispatch_summary");
        builder.HasKey(x => x.Day);
    }
}
