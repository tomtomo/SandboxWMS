using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate PutawayTask (DDD persistence; ADR-0010)
// Why: PutawayTask mereferensikan Stock by id (StockId) — bukan navigation/FK lintas aggregate
// (DDD: referensikan aggregate lain via identitas). stock_id disimpan sebagai Guid (value-converted),
// tanpa constraint FK ke stocks. Field 03b: source/suggested/actual destination + assignedTo.
public sealed class PutawayTaskConfiguration : IEntityTypeConfiguration<PutawayTask>
{
    public void Configure(EntityTypeBuilder<PutawayTask> builder)
    {
        builder.ToTable("putaway_tasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new PutawayTaskId(value))
            .ValueGeneratedNever();

        builder.Property(t => t.StockId)
            .HasConversion(id => id.Value, value => new StockId(value))
            .IsRequired();

        builder.Property(t => t.SourceLocationId).HasMaxLength(64).IsRequired();
        builder.Property(t => t.SuggestedDestinationId).HasMaxLength(64).IsRequired();
        builder.Property(t => t.AssignedTo).HasMaxLength(64);
        builder.Property(t => t.ActualDestinationId).HasMaxLength(64);

        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
    }
}
