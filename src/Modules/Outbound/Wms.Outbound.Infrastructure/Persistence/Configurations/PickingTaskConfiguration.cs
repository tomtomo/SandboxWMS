using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate PickingTask (DDD persistence; ADR-0010)
// Why: PickingTask mereferensikan Wave & Stock by id (Guid) — bukan navigation/FK lintas aggregate (DDD;
// Stock bahkan milik Inventory, DB-per-service). Membawa snapshot alokasi (source/sku/batch/qty). Index
// wave_id mendukung gate Wave→Ready (ListByWaveAsync di CompletePicking).
public sealed class PickingTaskConfiguration : IEntityTypeConfiguration<PickingTask>
{
    public void Configure(EntityTypeBuilder<PickingTask> builder)
    {
        builder.ToTable("picking_tasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new PickingTaskId(value))
            .ValueGeneratedNever();

        builder.Property(t => t.WaveId).IsRequired();
        builder.Property(t => t.StockId).IsRequired();
        builder.Property(t => t.SourceLocationId).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Sku).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Batch).HasMaxLength(64);
        builder.Property(t => t.Qty).IsRequired();
        builder.Property(t => t.AssignedTo).HasMaxLength(64);
        builder.Property(t => t.ActualQty);
        builder.Property(t => t.StagingLocationId).HasMaxLength(64);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // gate Wave→Ready: semua PickingTask terikat wave (CompletePicking)
        builder.HasIndex(t => t.WaveId);
    }
}
