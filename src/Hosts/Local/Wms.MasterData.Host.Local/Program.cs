using Microsoft.AspNetCore.Server.Kestrel.Core;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.BuildingBlocks.Web.Security;
using Wms.MasterData.Api;
using Wms.MasterData.Api.Grpc;
using Wms.MasterData.Application.DependencyInjection;
using Wms.MasterData.Infrastructure.DependencyInjection;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience (split-timeout) + OTel (ADR-0008)
builder.AddServiceDefaults();

// MasterData = supporting authority (overview §D): expose gRPC read-API (service-to-service, ADR-0006/
// 0011) + REST CRUD (manajemen). gRPC butuh HTTP/2; REST HTTP/1.1 → Kestrel Http1AndHttp2 (h2c di Local,
// Kestrel sniff preface koneksi). READ-ONLY ke core: TANPA rail messaging/outbox/consumer (tak emit/
// consume event di core flow); audit_log dipakai utk CRUD writes. Cross-process gRPC delivery di Local =
// live Aspire (manual); E2E otoritatif via integration test 1-proses.
builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(listen => listen.Protocols = HttpProtocols.Http1AndHttp2));

var masterDataConnection = builder.Configuration.GetConnectionString("masterdatadb")
    ?? throw new InvalidOperationException("ConnectionStrings:masterdatadb tidak diset (Aspire WithReference).");

builder.Services.AddMasterDataInfrastructure(masterDataConnection);
builder.Services.AddMasterDataApplication();
builder.Services.AddLocalCaching();           // ICacheStore untuk cache-aside read-API
builder.Services.AddHttpContextCurrentUser(); // identitas operator REST CRUD → IAuditable (created_by)
builder.Services.AddLocalAuditing();

// Phase 04b: validasi user JWT OFFLINE (ADR-0016 alg-pin RS256) — public key via ISecretProvider (env
// AppHost / ephemeral Local). Token valid → HttpContext.User terisi → ICurrentUser identitas NYATA (ganti
// anonymous) → audit created_by nyata utk REST CRUD. authZ tetap deferred (ADR-0012, tanpa [Authorize]).
builder.Services.AddLocalSecretProvider();
builder.Services.AddWmsJwtBearer();

// gRPC server + interceptor Result→RpcException (ADR-0019: status mapping tak tersebar di service)
builder.Services.AddGrpc(options => options.Interceptors.Add<ResultExceptionInterceptor>());

var app = builder.Build();

// correlation-id sedini mungkin → tiap log/trace/audit berbagi korelator (ADR-0024 baseline)
app.UseCorrelationId();

// authentication: validasi bearer offline → isi principal sebelum endpoint/handler (ICurrentUser nyata, 04b)
app.UseAuthentication();

app.MapGrpcService<MasterDataReadService>();
app.MapMasterDataEndpoints();
app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.MasterData.Host.Local — Phase 04a (gRPC read-API + REST CRUD + cache-aside)");

app.Run();

// penanda kelas Program untuk WebApplicationFactory<Program> di integration test
public partial class Program;
