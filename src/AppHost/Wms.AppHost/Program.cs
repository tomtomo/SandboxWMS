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
    .WaitFor(authDb);

// Phase 04a: MasterData = supporting authority (gRPC read-API + REST CRUD + cache-aside). Dideklarasi
// agar core host bisa WithReference (service discovery "masterdata"). + public key untuk verify user JWT.
var masterdata = builder.AddProject<Projects.Wms_MasterData_Host_Local>("masterdata")
    .WithReference(masterdataDb)
    .WithEnvironment(PublicKeyEnv, jwtPublicKeyPem)
    .WaitFor(masterdataDb);

// Core flow (Phase 03c): Inbound + Inventory + Outbound. Tiap host: DB sendiri + public key (validasi
// user JWT offline → ICurrentUser identitas nyata, ADR-0016/0027) + ref masterdata (snapshot uom gRPC).
builder.AddProject<Projects.Wms_Inbound_Host_Local>("inbound")
    .WithReference(inboundDb)
    .WithReference(masterdata)
    .WithEnvironment(PublicKeyEnv, jwtPublicKeyPem)
    .WaitFor(inboundDb);

builder.AddProject<Projects.Wms_Inventory_Host_Local>("inventory")
    .WithReference(inventoryDb)
    .WithReference(masterdata)
    .WithEnvironment(PublicKeyEnv, jwtPublicKeyPem)
    .WaitFor(inventoryDb);

builder.AddProject<Projects.Wms_Outbound_Host_Local>("outbound")
    .WithReference(outboundDb)
    .WithReference(masterdata)
    .WithEnvironment(PublicKeyEnv, jwtPublicKeyPem)
    .WaitFor(outboundDb);

// Phase 04c: Reporting = pure consumer (ADR-0017) — projection read-side dari domain event core via eventual
// consistency + query REST. DB-per-service (reportingdb). TAK butuh public key (authZ read deferred → 07a)
// maupun ref masterdata (semua dimensi projeksi ter-bawa di payload event, ADR-0030 — nol sync-query).
builder.AddProject<Projects.Wms_Reporting_Host_Local>("reporting")
    .WithReference(reportingDb)
    .WaitFor(reportingDb);

// Phase 04d: Notification = pure consumer (ADR-0017) — consume event core → subscription → enqueue
// delivery + worker async dispatch ke channel (idempotency + retry→DLQ). DB-per-service (notificationdb).
// Ref auth + masterdata: resolve recipient detail (Auth read-API) + warehouse context (MasterData read-API)
// via gRPC (service discovery). TAK butuh public key (authZ read deferred → 07a; s2s token Local stub).
builder.AddProject<Projects.Wms_Notification_Host_Local>("notification")
    .WithReference(notificationDb)
    .WithReference(auth)
    .WithReference(masterdata)
    .WaitFor(notificationDb);

builder.Build().Run();
