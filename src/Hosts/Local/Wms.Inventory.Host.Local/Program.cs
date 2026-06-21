using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Messaging;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;
using Wms.Platform.Local.Messaging;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience (ADR-0008)
builder.AddServiceDefaults();

// Inventory = consumer modul: DbContext (inventory + inbox/outbox) + adapter Local +
// Outbox dispatcher (idle di 01c — Inventory belum emit; aktif saat StockAllocated, Phase 03).
var inventoryConnection = builder.Configuration.GetConnectionString("inventorydb")
    ?? throw new InvalidOperationException("ConnectionStrings:inventorydb tidak diset (Aspire WithReference).");

builder.Services.AddInventoryInfrastructure(inventoryConnection);
builder.Services.AddLocalMessaging();
builder.Services.AddOutboxDispatcher();

var app = builder.Build();

// What: consumer subscribe-point (ADR-0029) — sambungkan dispatcher Inventory ke rail Local.
// Why: di Local 2-proses (Opsi C) ini IDLE — Inbound publish ke InMemoryMessagePublisher
// proses-nya sendiri; cross-process delivery menyusul via adapter broker (Phase 05/06).
// Choreography E2E dibuktikan via integration test 1-proses. Di cloud, adapter broker yang
// memanggil dispatcher.HandleAsync per pesan — kontraknya sama.
var publisher = app.Services.GetRequiredService<InMemoryMessagePublisher>();
var dispatcher = app.Services.GetRequiredService<InventoryIntegrationEventDispatcher>();
publisher.Subscribe(dispatcher.HandleAsync);

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Inventory.Host.Local — Phase 01c (GRConfirmed consumer + Inbox)");

app.Run();
