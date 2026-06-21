using Wms.Platform.Hosting;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience (ADR-0008)
builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Inbound.Host.Local — walking skeleton (Phase 01a)");

app.Run();
