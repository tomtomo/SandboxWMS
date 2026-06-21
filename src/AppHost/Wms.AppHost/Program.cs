var builder = DistributedApplication.CreateBuilder(args);

// Postgres container (DB-per-service: tiap service punya database sendiri di server ini — ADR-0010).
var postgres = builder.AddPostgres("postgres");
var inboundDb = postgres.AddDatabase("inbounddb");
var inventoryDb = postgres.AddDatabase("inventorydb");
var outboundDb = postgres.AddDatabase("outbounddb");
var masterdataDb = postgres.AddDatabase("masterdatadb");

// Phase 03c core flow lengkap: Inbound + Inventory + Outbound = tiga host terpisah (ADR-0008/0029),
// masing-masing DB sendiri (DB-per-service, ADR-0010). Cross-process delivery via broker = Phase 05/06;
// di Local choreography E2E dibuktikan lewat integration test 1-proses (Opsi C).
// Phase 04a: MasterData = supporting authority (gRPC read-API + REST CRUD + cache-aside). DB-per-service
// sendiri (masterdatadb). Dideklarasi DULU agar core host (Inbound/Outbound) bisa WithReference (service
// discovery "masterdata") untuk gRPC client SINKRON read-API.
var masterdata = builder.AddProject<Projects.Wms_MasterData_Host_Local>("masterdata")
    .WithReference(masterdataDb)
    .WaitFor(masterdataDb);

builder.AddProject<Projects.Wms_Inbound_Host_Local>("inbound")
    .WithReference(inboundDb)
    .WithReference(masterdata)
    .WaitFor(inboundDb);

builder.AddProject<Projects.Wms_Inventory_Host_Local>("inventory")
    .WithReference(inventoryDb)
    .WithReference(masterdata)
    .WaitFor(inventoryDb);

builder.AddProject<Projects.Wms_Outbound_Host_Local>("outbound")
    .WithReference(outboundDb)
    .WithReference(masterdata)
    .WaitFor(outboundDb);

builder.Build().Run();
