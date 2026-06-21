using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.Inbound.Api;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience (ADR-0008)
builder.AddServiceDefaults();

// Rail messaging (Phase 01b): DbContext modul (outbox/inbox/dead_letter) + adapter Local
// (publisher in-proc + DLQ Postgres) + Outbox dispatcher. Connection string di-inject
// Aspire via WithReference(inbounddb). Migration di-apply terpisah oleh MigrationRunner.
var inboundConnection = builder.Configuration.GetConnectionString("inbounddb")
    ?? throw new InvalidOperationException("ConnectionStrings:inbounddb tidak diset (Aspire WithReference).");

builder.Services.AddInboundInfrastructure(inboundConnection);
builder.Services.AddLocalMessaging();
builder.Services.AddOutboxDispatcher();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Inbound.Host.Local — Phase 01c (GoodsReceipt slice + Outbox)");
app.MapInboundEndpoints();

app.Run();
