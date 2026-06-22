using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;
using Wms.Platform.Local.Messaging;
using Wms.Reporting.DependencyInjection;
using Wms.Reporting.Endpoints;
using Wms.Reporting.Messaging;
using Wms.Reporting.Projectors;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience + OTel (ADR-0008)
builder.AddServiceDefaults();

// Reporting = PURE CONSUMER (ADR-0017): consume 4 event lintas-context → projection denormalized (schema
// "reporting") + query REST read-side. TAK emit (tanpa OutboxDispatcher); TAK auditable (tanpa AuditLogStore);
// authZ deferred (read-only, TODO-AUTH → 07a). DbContext (reporting + inbox/dead_letter) + adapter Local
// (IDeadLetterStore) + consumer dead-lettering. Profil serverless (event-triggered) → Functions 05d / 06d.
var reportingConnection = builder.Configuration.GetConnectionString("reportingdb")
    ?? throw new InvalidOperationException("ConnectionStrings:reportingdb tidak diset (Aspire WithReference).");

builder.Services.AddReporting(reportingConnection);
builder.Services.AddLocalMessaging();
builder.Services.AddConsumerDeadLettering();

var app = builder.Build();

// correlation-id sedini mungkin → tiap log/trace request berbagi korelator (ADR-0024 baseline)
app.UseCorrelationId();

// What: consumer subscribe-point (ADR-0029) — sambungkan dispatcher Reporting ke rail Local, PER event.
// Why: di Local 2-proses (Opsi C) ini IDLE — cross-process delivery menyusul via adapter broker (Phase
// 05d/06d serverless). Choreography E2E dibuktikan via integration test 1-proses. Tiap projector
// di-subscribe terpisah dengan DLQ source = HandlerType-nya → poison ter-atribusi tepat (forensik granular).
// How: tiap dispatcher.HandleXxxAsync dibungkus ConsumerDeadLetterPipeline (retry → DLQ, Phase 02b).
var publisher = app.Services.GetRequiredService<InMemoryMessagePublisher>();
var dispatcher = app.Services.GetRequiredService<ReportingIntegrationEventDispatcher>();
var deadLettering = app.Services.GetRequiredService<ConsumerDeadLetterPipeline>();
publisher.Subscribe(deadLettering.Wrap(
    GoodsReceiptConfirmedProjector.HandlerType, dispatcher.HandleGoodsReceiptConfirmedAsync));
publisher.Subscribe(deadLettering.Wrap(
    StockRemovedProjector.HandlerType, dispatcher.HandleStockRemovedAsync));
publisher.Subscribe(deadLettering.Wrap(
    PutawayCompletedProjector.HandlerType, dispatcher.HandlePutawayCompletedAsync));
publisher.Subscribe(deadLettering.Wrap(
    PickingCompletedProjector.HandlerType, dispatcher.HandlePickingCompletedAsync));

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Reporting.Host.Local — Phase 04c (CQRS read-side projections + query API)");
app.MapReportingEndpoints();

app.Run();

// penanda untuk WebApplicationFactory<Program> (integration test query endpoint in-proc)
public partial class Program;
