var builder = DistributedApplication.CreateBuilder(args);

// Postgres container (DB-per-service: tiap service punya database sendiri di server ini — ADR-0010).
var postgres = builder.AddPostgres("postgres");
var inboundDb = postgres.AddDatabase("inbounddb");

// Walking skeleton: baru Inbound host. Service lain menyusul di phase berikut.
builder.AddProject<Projects.Wms_Inbound_Host_Local>("inbound")
    .WithReference(inboundDb)
    .WaitFor(inboundDb);

builder.Build().Run();
