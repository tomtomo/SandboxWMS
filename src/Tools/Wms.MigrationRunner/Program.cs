using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Inbound.Infrastructure.Persistence;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Persistence;
using Wms.Outbound.Infrastructure.DependencyInjection;
using Wms.Outbound.Infrastructure.Persistence;
using Wms.MasterData.Infrastructure.DependencyInjection;
using Wms.MasterData.Infrastructure.Persistence;
using Wms.Auth.Infrastructure.DependencyInjection;
using Wms.Auth.Infrastructure.Persistence;
using Wms.Auth.Infrastructure.Security;
using Wms.Platform.Local.DependencyInjection;

// What: MigrationRunner — env-neutral migration applier (ADR-0010 amendment)
// Why: menutup lubang operasional "N migration assembly diterapkan bagaimana" pada
// topologi DB-per-service. Satu console yang meng-apply EF migration ke SETIAP service
// DB; connection string di-inject via config (appsettings/env/cmdline), nol cloud SDK.
// How: generic host untuk config+DI+logging; daftarkan Infrastructure tiap modul lalu
// MigrateAsync masing-masing DbContext dalam scope sendiri.
var builder = Host.CreateApplicationBuilder(args);

var inboundConnection = builder.Configuration.GetConnectionString("inbounddb")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:inbounddb tidak diset (appsettings.json / env / --ConnectionStrings:inbounddb=...).");

builder.Services.AddInboundInfrastructure(inboundConnection);

var inventoryConnection = builder.Configuration.GetConnectionString("inventorydb")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:inventorydb tidak diset (appsettings.json / env / --ConnectionStrings:inventorydb=...).");

builder.Services.AddInventoryInfrastructure(inventoryConnection);

var outboundConnection = builder.Configuration.GetConnectionString("outbounddb")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:outbounddb tidak diset (appsettings.json / env / --ConnectionStrings:outbounddb=...).");

builder.Services.AddOutboundInfrastructure(outboundConnection);

var masterDataConnection = builder.Configuration.GetConnectionString("masterdatadb")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:masterdatadb tidak diset (appsettings.json / env / --ConnectionStrings:masterdatadb=...).");

builder.Services.AddMasterDataInfrastructure(masterDataConnection);

var authConnection = builder.Configuration.GetConnectionString("authdb")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:authdb tidak diset (appsettings.json / env / --ConnectionStrings:authdb=...).");

builder.Services.AddAuthInfrastructure(authConnection);
// IPasswordHasher (Argon2id) untuk seed admin Auth — DB-prep tool yang sama meng-apply migrasi & seed.
builder.Services.AddLocalPasswordHasher();

using var host = builder.Build();

await ApplyMigrationsAsync<InboundDbContext>(host, "Inbound");
await ApplyMigrationsAsync<InventoryDbContext>(host, "Inventory");
await ApplyMigrationsAsync<OutboundDbContext>(host, "Outbound");
await ApplyMigrationsAsync<MasterDataDbContext>(host, "MasterData");
await ApplyMigrationsAsync<AuthDbContext>(host, "Auth");

// What: seed reference/admin Auth (ADR-0012) — DB-prep = migrate schema + seed seed-data, idempoten.
using (var scope = host.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await scope.ServiceProvider.GetRequiredService<AuthSeeder>().SeedAsync();
    logger.LogInformation("Auth: seed permission catalog + admin selesai.");
}

return 0;

static async Task ApplyMigrationsAsync<TContext>(IHost host, string module)
    where TContext : DbContext
{
    using var scope = host.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<TContext>();

    var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
    if (pending.Length == 0)
    {
        logger.LogInformation("{Module}: tak ada migration tertunda.", module);
        return;
    }

    logger.LogInformation("{Module}: menerapkan {Count} migration ({Names})…",
        module, pending.Length, string.Join(", ", pending));
    await db.Database.MigrateAsync();
    logger.LogInformation("{Module}: migrasi selesai.", module);
}

// penanda kelas Program untuk ILogger<Program> di top-level statements
public partial class Program;
