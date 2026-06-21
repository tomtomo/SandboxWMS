using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.BuildingBlocks.Web.Security;
using Wms.Inbound.Api;
using Wms.Inbound.Application.DependencyInjection;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience + OTel (ADR-0008)
builder.AddServiceDefaults();

// Rail messaging (Phase 01b): DbContext modul (outbox/inbox/dead_letter/audit_log) + adapter
// Local (publisher in-proc + DLQ Postgres) + Outbox dispatcher. Connection string di-inject
// Aspire via WithReference(inbounddb). Migration di-apply terpisah oleh MigrationRunner.
var inboundConnection = builder.Configuration.GetConnectionString("inbounddb")
    ?? throw new InvalidOperationException("ConnectionStrings:inbounddb tidak diset (Aspire WithReference).");

builder.Services.AddInboundApplication();
builder.Services.AddInboundInfrastructure(inboundConnection);
builder.Services.AddLocalMessaging();
builder.Services.AddOutboxDispatcher();

// Phase 02c: host HTTP → identitas dari HttpContext/JWT (ICurrentUser) + audit-log store Local.
// AuditLogBehavior (pipeline Inbound) menulis audit out-of-band; IAuditable diisi interceptor.
builder.Services.AddHttpContextCurrentUser();
builder.Services.AddLocalAuditing();

var app = builder.Build();

// correlation-id sedini mungkin → tiap log/trace/audit request berbagi korelator (ADR-0024 baseline)
app.UseCorrelationId();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Inbound.Host.Local — Phase 02c (GoodsReceipt + Outbox + audit + OTel)");
app.MapInboundEndpoints();

app.Run();
