var builder = DistributedApplication.CreateBuilder(args);

// Postgres container (DB-per-service: tiap service punya database sendiri di server ini — ADR-0010).
var postgres = builder.AddPostgres("postgres");
var inboundDb = postgres.AddDatabase("inbounddb");
var inventoryDb = postgres.AddDatabase("inventorydb");

// Phase 01c walking skeleton: Inbound (producer) + Inventory (consumer) = dua host terpisah
// (ADR-0008/0029). Cross-process delivery via broker = Phase 05/06; di Local choreography
// E2E dibuktikan lewat integration test 1-proses (Opsi C).
builder.AddProject<Projects.Wms_Inbound_Host_Local>("inbound")
    .WithReference(inboundDb)
    .WaitFor(inboundDb);

builder.AddProject<Projects.Wms_Inventory_Host_Local>("inventory")
    .WithReference(inventoryDb)
    .WaitFor(inventoryDb);

builder.Build().Run();
