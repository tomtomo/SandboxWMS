using System.Security.Cryptography;

var builder = DistributedApplication.CreateBuilder(args);

// Postgres container (DB-per-service: tiap service punya database sendiri di server ini — ADR-0010).
var postgres = builder.AddPostgres("postgres");
var inboundDb = postgres.AddDatabase("inbounddb");
var inventoryDb = postgres.AddDatabase("inventorydb");
var outboundDb = postgres.AddDatabase("outbounddb");
var masterdataDb = postgres.AddDatabase("masterdatadb");
var authDb = postgres.AddDatabase("authdb");
var reportingDb = postgres.AddDatabase("reportingdb");
var notificationDb = postgres.AddDatabase("notificationdb");

// RabbitMQ broker LOKAL (ADR-0029 amendment): cross-process domain-event delivery NYATA di Local Aspire —
// mengaktifkan subscribe-point yang dulu IDLE (in-proc). Outbox tiap producer → exchange topic "wms.events";
// queue durable per modul consumer. Management plugin (UI) untuk inspeksi exchange/queue/DLQ saat dev.
// Connection string di-inject ke host event-driven (inbound/inventory/outbound/reporting/notification).
var rabbitmq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();

// Phase 04e: MigrationRunner = DB-prep resource (apply EF migration ke 7 DB-per-service + seed admin/permission,
// ADR-0010/0012). Pola Aspire/eShop: run-to-completion; service WaitForCompletion(migrations) di bawah → DB
// Aspire (container, connection string DINAMIS) ter-migrate & ter-seed otomatis SEBELUM login/flow, jadi DoD
// smoke E2E lokal jalan tanpa langkah manual. Connection string per-DB di-inject Aspire (override appsettings
// localhost MigrationRunner). Tanpa public key (murni DB-prep, tak menerbitkan/memvalidasi JWT).
var migrations = builder.AddProject<Projects.Wms_MigrationRunner>("migrations")
    .WithReference(inboundDb).WaitFor(inboundDb)
    .WithReference(inventoryDb).WaitFor(inventoryDb)
    .WithReference(outboundDb).WaitFor(outboundDb)
    .WithReference(masterdataDb).WaitFor(masterdataDb)
    .WithReference(authDb).WaitFor(authDb)
    .WithReference(reportingDb).WaitFor(reportingDb)
    .WithReference(notificationDb).WaitFor(notificationDb);

// Phase 04b: dev RSA keypair RS256 (ADR-0016) di-generate SEKALI per-run AppHost & DIDISTRIBUSI via env.
// private key (signing) HANYA ke auth host; public key (verify OFFLINE) ke SEMUA host → validasi user-JWT
// lintas-host jalan beneran di Local Aspire. Ephemeral per-run = NOL credential di source (credential
// hygiene); cloud (Key Vault/Secret Manager) menggantikan distribusi ini di Phase 05/07.
using var rsa = RSA.Create(2048);
var jwtSigningKeyPem = rsa.ExportPkcs8PrivateKeyPem();
var jwtPublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
const string SigningKeyEnv = "Secrets__auth-jwt-signing-key";
const string PublicKeyEnv = "Secrets__auth-jwt-public-key";

// Phase 04b: Auth = supporting authority (overview §E) — JWT RS256 + refresh rotation + Argon2id +
// read-API. DB-per-service (authdb). Dapat private signing key (issuer) + public key (validasi sendiri).
var auth = builder.AddProject<Projects.Wms_Auth_Host_Local>("auth")
    .WithReference(authDb)
    .WithEnvironment(SigningKeyEnv, jwtSigningKeyPem)
    .WithEnvironment(PublicKeyEnv, jwtPublicKeyPem)
    .WaitFor(authDb)
    .WaitForCompletion(migrations);

// Phase 04a: MasterData = supporting authority (gRPC read-API + REST CRUD + cache-aside). Dideklarasi
// agar core host bisa WithReference (service discovery "masterdata"). + public key untuk verify user JWT.
var masterdata = builder.AddProject<Projects.Wms_MasterData_Host_Local>("masterdata")
    .WithReference(masterdataDb)
    .WithEnvironment(PublicKeyEnv, jwtPublicKeyPem)
    .WaitFor(masterdataDb)
    .WaitForCompletion(migrations);

// Core flow (Phase 03c): Inbound + Inventory + Outbound. Tiap host: DB sendiri + public key (validasi
// user JWT offline → ICurrentUser identitas nyata, ADR-0016/0027) + ref masterdata (snapshot uom gRPC).
var inbound = builder.AddProject<Projects.Wms_Inbound_Host_Local>("inbound")
    .WithReference(inboundDb)
    .WithReference(masterdata)
    .WithEnvironment(PublicKeyEnv, jwtPublicKeyPem)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WaitFor(inboundDb)
    .WaitForCompletion(migrations);

var inventory = builder.AddProject<Projects.Wms_Inventory_Host_Local>("inventory")
    .WithReference(inventoryDb)
    .WithReference(masterdata)
    .WithEnvironment(PublicKeyEnv, jwtPublicKeyPem)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WaitFor(inventoryDb)
    .WaitForCompletion(migrations);

var outbound = builder.AddProject<Projects.Wms_Outbound_Host_Local>("outbound")
    .WithReference(outboundDb)
    .WithReference(masterdata)
    .WithEnvironment(PublicKeyEnv, jwtPublicKeyPem)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WaitFor(outboundDb)
    .WaitForCompletion(migrations);

// Phase 04c: Reporting = pure consumer (ADR-0017) — projection read-side dari domain event core via eventual
// consistency + query REST. DB-per-service (reportingdb). Projection PATH tetap nol sync-query (semua dimensi
// ter-bawa payload event, ADR-0030). Ref auth: enrichment-at-READ — operator-activity me-resolve OperatorId→
// username via Auth read-API gRPC (ACL), bukan denormalize ke projection (username Auth-owned & mutable). TAK
// butuh public key (authZ read deferred → 07a; s2s token Local stub).
var reporting = builder.AddProject<Projects.Wms_Reporting_Host_Local>("reporting")
    .WithReference(reportingDb)
    .WithReference(auth)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WaitFor(reportingDb)
    .WaitForCompletion(migrations);

// Phase 04d: Notification = pure consumer (ADR-0017) — consume event core → subscription → enqueue
// delivery + worker async dispatch ke channel (idempotency + retry→DLQ). DB-per-service (notificationdb).
// Ref auth + masterdata: resolve recipient detail (Auth read-API) + warehouse context (MasterData read-API)
// via gRPC (service discovery). TAK butuh public key (authZ read deferred → 07a; s2s token Local stub).
var notification = builder.AddProject<Projects.Wms_Notification_Host_Local>("notification")
    .WithReference(notificationDb)
    .WithReference(auth)
    .WithReference(masterdata)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WaitFor(notificationDb)
    .WaitForCompletion(migrations);

// Phase 04e: Gateway = reverse-proxy YARP lokal (ADR-0006/0018) — routing REST UI→service + cross-cutting
// (auth-forward bearer, correlation-id), TANPA transcoding. WithReference ke tiap service yang di-route
// supaya destination "http://<service>" ter-resolve via Aspire service discovery (resolver YARP).
var gateway = builder.AddProject<Projects.Wms_Gateway>("gateway")
    .WithReference(auth)
    .WithReference(inbound)
    .WithReference(inventory)
    .WithReference(outbound)
    .WithReference(masterdata)
    .WithReference(reporting)
    .WithReference(notification);

// Phase 04e: WebUI = Blazor Server thin (ADR-0018 stateful circuit) — UI panggil REST HANYA lewat gateway
// (ADR-0006: UI tak panggil gRPC/service langsung). WithReference(gateway) → base address "http://gateway".
builder.AddProject<Projects.Wms_WebUI>("webui")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
