using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.BuildingBlocks.Web.Security;
using Wms.Outbound.Api;
using Wms.Outbound.Application.DependencyInjection;
using Wms.Outbound.Application.Features.ConsumeStockAllocated;
using Wms.Outbound.Infrastructure.DependencyInjection;
using Wms.Outbound.Infrastructure.Messaging;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;
using Wms.Platform.Local.Messaging;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience + OTel (ADR-0008)
builder.AddServiceDefaults();

// Outbound = modul HYBRID (Phase 03c): producer 3 event (WaveReleased/PickingCompleted/ShipmentDispatched
// via Outbox) + consumer StockAllocated (→ PickingTask) + REST SPV/operator. DbContext (outbound + inbox/
// outbox/dead_letter/audit_log) + adapter Local + Outbox dispatcher (AKTIF) + consumer dead-lettering.
var outboundConnection = builder.Configuration.GetConnectionString("outbounddb")
    ?? throw new InvalidOperationException("ConnectionStrings:outbounddb tidak diset (Aspire WithReference).");

builder.Services.AddOutboundInfrastructure(outboundConnection);
builder.Services.AddOutboundApplication();
builder.Services.AddLocalMessaging();
builder.Services.AddOutboxDispatcher();
builder.Services.AddConsumerDeadLettering();

// host melayani REST → identitas dari HttpContext/JWT (ICurrentUser) untuk CreateWave/CompletePicking/
// DispatchWave + audit-log store Local (AuditLogBehavior). KONSUMER tetap origin-MESIN: scope event (tanpa
// HttpContext) → HttpContextCurrentUser resolve SYSTEM (ADR-0027) → created_by=SYSTEM saat consumer menulis
// PickingTask; request REST membawa identitas operator. Satu host, dua origin — aman.
builder.Services.AddHttpContextCurrentUser();
builder.Services.AddLocalAuditing();

var app = builder.Build();

// correlation-id sedini mungkin → tiap log/trace/audit request berbagi korelator (ADR-0024 baseline)
app.UseCorrelationId();

// What: consumer subscribe-point (ADR-0029) — sambungkan dispatcher Outbound ke rail Local, PER event.
// Why: di Local 3-proses ini IDLE — cross-process delivery menyusul via adapter broker (Phase 05/06).
// Choreography E2E dibuktikan via integration test 1-proses. Consumer di-subscribe dengan DLQ source =
// HandlerType-nya → poison ter-atribusi ke consumer yang benar (forensik granular).
// How: dispatcher.HandleStockAllocatedAsync dibungkus ConsumerDeadLetterPipeline (retry → DLQ, Phase 02b).
var publisher = app.Services.GetRequiredService<InMemoryMessagePublisher>();
var dispatcher = app.Services.GetRequiredService<OutboundIntegrationEventDispatcher>();
var deadLettering = app.Services.GetRequiredService<ConsumerDeadLetterPipeline>();
publisher.Subscribe(deadLettering.Wrap(
    StockAllocatedConsumer.HandlerType, dispatcher.HandleStockAllocatedAsync));

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Outbound.Host.Local — Phase 03c (OutboundOrder/Wave/PickingTask + 3 event emit + StockAllocated consumer)");
app.MapOutboundEndpoints();

app.Run();
