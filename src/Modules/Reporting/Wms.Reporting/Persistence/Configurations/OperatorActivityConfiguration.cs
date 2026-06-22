using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Reporting.Projections;

namespace Wms.Reporting.Persistence.Configurations;

// What: EF mapping projection OperatorActivity (denormalized read-model; ADR-0017)
// Why: PK komposit (operator, hari) = bucket produktivitas. OperatorId NON-NULL ("" SYSTEM/tak diketahui).
public sealed class OperatorActivityConfiguration : IEntityTypeConfiguration<OperatorActivity>
{
    public void Configure(EntityTypeBuilder<OperatorActivity> builder)
    {
        builder.ToTable("operator_activity");
        builder.HasKey(x => new { x.OperatorId, x.Day });
        builder.Property(x => x.OperatorId).HasMaxLength(200);
    }
}
