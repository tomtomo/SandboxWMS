using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence.Configurations;

// What: EF Core mapping aggregate Wave (DDD persistence; ADR-0010)
// Why: Wave mereferensikan order & picking task LAIN via identitas (Guid), bukan navigation/FK lintas
// aggregate (DDD). orderIds & pickingTaskIds = koleksi skalar Guid → dipetakan EF Core 8 PRIMITIVE
// COLLECTION (uuid[] di Postgres via Npgsql) lewat BACKING FIELD, bukan tabel join terpisah — idiomatik
// EF8 untuk scalar list yang dimiliki penuh aggregate. Status → string.
// How: properti publik OrderIds/PickingTaskIds read-only (IReadOnlyCollection) tak bisa jadi primitive
// collection langsung (EF menolak read-only) → di-Ignore, lalu backing field MUTABLE (List<Guid>) yang
// dipetakan by-name. Enkapsulasi domain terjaga (domain expose read-only); EF tulis/baca List konkret.
public sealed class WaveConfiguration : IEntityTypeConfiguration<Wave>
{
    public void Configure(EntityTypeBuilder<Wave> builder)
    {
        builder.ToTable("waves");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => new WaveId(value))
            .ValueGeneratedNever();

        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // read-only accessor di-Ignore; backing field mutable yang dipetakan jadi uuid[] (primitive collection)
        builder.Ignore(w => w.OrderIds);
        builder.Ignore(w => w.PickingTaskIds);
        builder.PrimitiveCollection<List<Guid>>("_orderIds").HasColumnName("order_ids");
        builder.PrimitiveCollection<List<Guid>>("_pickingTaskIds").HasColumnName("picking_task_ids");
    }
}
