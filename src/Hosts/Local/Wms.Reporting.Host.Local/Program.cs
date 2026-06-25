using Wms.Auth.Grpc;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.BuildingBlocks.Infrastructure.Resilience;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.BuildingBlocks.Web.Grpc;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;
using Wms.Reporting.DependencyInjection;
using Wms.Reporting.Directory;
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

// messaging transport (ADR-0029 amendment): RabbitMQ broker bila ConnectionStrings:rabbitmq tersedia (Aspire)
// → consume 4 event lintas-proses NYATA → projection; else in-proc fallback (integration test 1-proses).
var rabbitConn = builder.Configuration.GetConnectionString("rabbitmq");
if (!string.IsNullOrWhiteSpace(rabbitConn))
    builder.Services.AddRabbitMqMessaging(rabbitConn, "reporting");
else
    builder.Services.AddLocalMessaging();

builder.Services.AddConsumerDeadLettering();

// gRPC read-API client Auth (enrichment-at-read OperatorId→username) — ADR-0011 + RESILIENCE split-timeout
// (ADR-0020 pipeline "wms-grpc") + s2s token (ADR-0021 Local stub kosong). Address via Aspire service discovery
// (AppHost reporting.WithReference(auth)). Adapter IUserDirectory di-wire host (DIPISAH dari AddReporting agar
// integration test bisa stub). Hanya read-path query yang sync-query Auth; projection PATH tetap nol sync-query
// (ADR-0030 — semua dimensi ter-bawa payload event).
builder.Services.AddGrpcResiliencePipeline();
builder.Services.AddLocalServiceTokenProvider();
builder.Services.AddWmsInternalGrpcClient<AuthReadApi.AuthReadApiClient>("https://auth", "auth");
builder.Services.AddReportingDirectories();

var app = builder.Build();

// correlation-id sedini mungkin → tiap log/trace request berbagi korelator (ADR-0024 baseline)
app.UseCorrelationId();

// What: consumer subscribe-point (ADR-0029 amendment) — sambungkan dispatcher Reporting ke rail, PER event.
// Why: kini AKTIF lintas-proses — IMessageSubscriber = adapter RabbitMQ (queue "reporting" bind "#") saat broker
// ada, atau in-proc saat fallback test. Tiap projector di-subscribe terpisah dengan DLQ source = HandlerType-nya
// → poison ter-atribusi tepat (forensik granular).
// How: tiap dispatcher.HandleXxxAsync dibungkus ConsumerDeadLetterPipeline (retry → DLQ, Phase 02b).
var subscriber = app.Services.GetRequiredService<IMessageSubscriber>();
var dispatcher = app.Services.GetRequiredService<ReportingIntegrationEventDispatcher>();
var deadLettering = app.Services.GetRequiredService<ConsumerDeadLetterPipeline>();
subscriber.Subscribe(deadLettering.Wrap(
    GoodsReceiptConfirmedProjector.HandlerType, dispatcher.HandleGoodsReceiptConfirmedAsync));
subscriber.Subscribe(deadLettering.Wrap(
    StockRemovedProjector.HandlerType, dispatcher.HandleStockRemovedAsync));
subscriber.Subscribe(deadLettering.Wrap(
    PutawayCompletedProjector.HandlerType, dispatcher.HandlePutawayCompletedAsync));
subscriber.Subscribe(deadLettering.Wrap(
    PickingCompletedProjector.HandlerType, dispatcher.HandlePickingCompletedAsync));

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Reporting.Host.Local — Phase 04c (CQRS read-side projections + query API)");
app.MapReportingEndpoints();

app.Run();

// penanda untuk WebApplicationFactory<Program> (integration test query endpoint in-proc)
public partial class Program;
