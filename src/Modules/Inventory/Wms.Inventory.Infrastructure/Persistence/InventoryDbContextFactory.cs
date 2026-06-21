using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wms.Inventory.Infrastructure.Persistence;

// What: design-time DbContext factory (EF tooling)
// Why: `dotnet ef migrations` perlu meng-instansiasi InventoryDbContext tanpa host
// runtime. Connection string placeholder — `migrations add` hanya membaca model.
// How: bangun options persis seperti runtime (UseNpgsql + snake_case) agar model
// scaffold identik dengan produksi.
public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=inventorydb;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", InventoryDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new InventoryDbContext(options);
    }
}
