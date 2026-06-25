using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inbound.Contracts;
using Wms.Inventory.Contracts;
using Wms.Outbound.Contracts;
using Wms.Reporting.Directory;
using Wms.Reporting.Endpoints;
using Wms.Reporting.Persistence;
using Wms.Reporting.Projectors;
using Wms.Reporting.Rebuild;
using Wms.TestSupport;

namespace Wms.Reporting.IntegrationTests;

// What: integration test Reporting (DoD Phase 04c, ADR-0017/0030) atas Postgres NYATA (Testcontainers)
// Why: membuktikan CQRS read-side projections di Postgres riil: (a) GRConfirmed→ReceivingSummary+StockOnHand;
// (b) Inbox-committed atomicity EXACTLY-ONCE (duplicate → no double-count); (c) StockRemoved decrement +
// DispatchSummary; (d) Putaway/Picking→OperatorActivity; (e) QUERY ENDPOINT REST kembalikan projection
// (WebApplicationFactory in-proc); (f) REBUILD-from-events (reset → replay → projection identik).
// How: WebApplicationFactory<Program> host Reporting = container DI + server HTTP; projector di-invoke
// langsung (mensimulasikan delivery rail) dengan eventId + occurredAt (dari envelope) eksplisit.
[Collection(PostgresCollection.Name)]
public sealed class ReportingProjectionTests(PostgresFixture fixture)
{
    private const string Warehouse = "WH-JKT";
    private static readonly DateTimeOffset At = new(2026, 6, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 6, 20);

    [Fact]
    public async Task GrConfirmed_projects_receiving_summary_and_stock_on_hand()
    {
        await using var factory = await CreateFactoryAsync();

        await ProjectGrConfirmedAsync(factory, Guid.NewGuid(), At, new GRConfirmedV1(
            Guid.NewGuid(), Warehouse, "SUP-1",
            [
                new ReceivedLineV1("SKU-1", 10, "Good", "B1", null),
                new ReceivedLineV1("SKU-2", 5, "QcHold", null, null),   // no batch → "" key
            ],
            [new RejectedLineV1("SKU-1", 2, "RejectExcess")]));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        var summary = await db.ReceivingSummaries.SingleAsync();
        Assert.Equal("SUP-1", summary.SupplierId);
        Assert.Equal(Day, summary.Day);
        Assert.Equal(1, summary.GrCount);
        Assert.Equal(15, summary.ReceivedQty);    // 10 + 5
        Assert.Equal(2, summary.RejectedQty);

        var views = await db.StockOnHandViews.OrderBy(v => v.Sku).ToListAsync();
        Assert.Equal(2, views.Count);
        Assert.Equal("B1", views[0].Batch);
        Assert.Equal(10, views[0].QtyOnHand);
        Assert.Equal(string.Empty, views[1].Batch);   // SKU-2 tanpa batch → "" key
        Assert.Equal(5, views[1].QtyOnHand);
    }

    [Fact]
    public async Task Duplicate_gr_confirmed_delivery_projects_exactly_once()
    {
        await using var factory = await CreateFactoryAsync();

        var eventId = Guid.NewGuid();
        var message = new GRConfirmedV1(
            Guid.NewGuid(), Warehouse, "SUP-1", [new ReceivedLineV1("SKU-1", 10, "Good", "B1", null)], []);

        await ProjectGrConfirmedAsync(factory, eventId, At, message);
        await ProjectGrConfirmedAsync(factory, eventId, At, message);   // redelivery (at-least-once) — eventId sama

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        Assert.Equal(1, (await db.ReceivingSummaries.SingleAsync()).GrCount);     // tak double-count
        Assert.Equal(10, (await db.StockOnHandViews.SingleAsync()).QtyOnHand);    // tak double-add
        Assert.Equal(1, await db.Set<InboxMessage>()
            .CountAsync(i => i.HandlerType == GoodsReceiptConfirmedProjector.HandlerType));   // diproses sekali
    }

    [Fact]
    public async Task StockRemoved_decrements_stock_on_hand_and_records_dispatch()
    {
        await using var factory = await CreateFactoryAsync();

        await ProjectGrConfirmedAsync(factory, Guid.NewGuid(), At, new GRConfirmedV1(
            Guid.NewGuid(), Warehouse, "SUP-1", [new ReceivedLineV1("SKU-1", 10, "Good", "B1", null)], []));
        await ProjectStockRemovedAsync(factory, Guid.NewGuid(), At, new StockRemovedV1(
            Guid.NewGuid(), [new StockRemovedLineV1(Warehouse, "SKU-1", "B1", 4)]));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        Assert.Equal(6, (await db.StockOnHandViews.SingleAsync()).QtyOnHand);     // 10 − 4

        var dispatch = await db.DispatchSummaries.SingleAsync();
        Assert.Equal(Day, dispatch.Day);
        Assert.Equal(1, dispatch.WaveCount);
        Assert.Equal(4, dispatch.TotalVolume);
    }

    [Fact]
    public async Task Putaway_and_picking_record_operator_activity()
    {
        await using var factory = await CreateFactoryAsync();

        await ProjectPutawayCompletedAsync(factory, Guid.NewGuid(), At, new PutawayCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-1", Warehouse, "op-1"));
        await ProjectPickingCompletedAsync(factory, Guid.NewGuid(), At, new PickingCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "B1", 5, "STG-1", "op-1"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        var activity = await db.OperatorActivities.SingleAsync();
        Assert.Equal("op-1", activity.OperatorId);
        Assert.Equal(Day, activity.Day);
        Assert.Equal(1, activity.PutawayCount);
        Assert.Equal(1, activity.PickCount);
    }

    [Fact]
    public async Task Operator_activity_endpoint_resolves_username_via_directory()
    {
        // stub Auth read-API: op-1 → "alice" (enrichment-at-read)
        var directory = new StubUserDirectory(new Dictionary<string, string> { ["op-1"] = "alice" });
        await using var factory = await CreateFactoryAsync(directory);

        await ProjectPutawayCompletedAsync(factory, Guid.NewGuid(), At, new PutawayCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-1", Warehouse, "op-1"));

        var client = factory.CreateClient();
        var result = await client
            .GetFromJsonAsync<PagedResult<OperatorActivityRow>>("/reports/operator-activity");

        var row = Assert.Single(result!.Items);
        Assert.Equal("op-1", row.OperatorId);
        Assert.Equal("alice", row.OperatorName);   // username ter-resolve, bukan id mentah
    }

    [Fact]
    public async Task Operator_activity_endpoint_falls_back_to_id_when_user_unknown()
    {
        // directory tak punya match → GetUsernameAsync null → fallback ke id mentah
        var directory = new StubUserDirectory(new Dictionary<string, string>());
        await using var factory = await CreateFactoryAsync(directory);

        await ProjectPickingCompletedAsync(factory, Guid.NewGuid(), At, new PickingCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "B1", 5, "STG-1", "op-9"));

        var client = factory.CreateClient();
        var result = await client
            .GetFromJsonAsync<PagedResult<OperatorActivityRow>>("/reports/operator-activity");

        var row = Assert.Single(result!.Items);
        Assert.Equal("op-9", row.OperatorId);
        Assert.Equal("op-9", row.OperatorName);   // fallback ke id (user tak ditemukan)
    }

    [Fact]
    public async Task Operator_activity_endpoint_shows_system_label_without_calling_directory()
    {
        // id sistem (authZ deferred → 07a): short-circuit ke "SYSTEM" TANPA tanya directory (peta sengaja
        // memetakan "SYSTEM"→"wrong"; bila directory dipanggil, assert akan gagal → membuktikan short-circuit).
        var directory = new StubUserDirectory(new Dictionary<string, string> { ["SYSTEM"] = "wrong" });
        await using var factory = await CreateFactoryAsync(directory);

        await ProjectPutawayCompletedAsync(factory, Guid.NewGuid(), At, new PutawayCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-1", Warehouse, "SYSTEM"));

        var client = factory.CreateClient();
        var result = await client
            .GetFromJsonAsync<PagedResult<OperatorActivityRow>>("/reports/operator-activity");

        var row = Assert.Single(result!.Items);
        Assert.Equal("SYSTEM", row.OperatorName);
    }

    [Fact]
    public async Task Query_endpoint_returns_stock_on_hand_projection()
    {
        await using var factory = await CreateFactoryAsync();

        await ProjectGrConfirmedAsync(factory, Guid.NewGuid(), At, new GRConfirmedV1(
            Guid.NewGuid(), Warehouse, "SUP-1", [new ReceivedLineV1("SKU-1", 10, "Good", "B1", null)], []));

        var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<PagedResult<StockOnHandRow>>("/reports/stock-on-hand");

        var row = Assert.Single(result!.Items);
        Assert.Equal(Warehouse, row.WarehouseId);
        Assert.Equal("SKU-1", row.Sku);
        Assert.Equal("B1", row.Batch);
        Assert.Equal(10, row.QtyOnHand);
    }

    [Fact]
    public async Task Rebuild_from_events_reconstructs_identical_projection()
    {
        await using var factory = await CreateFactoryAsync();

        await SeedStreamAsync(factory);
        var before = await SnapshotAsync(factory);
        Assert.NotEmpty(before);

        // RESET (rebuild): kosongkan projeksi + Inbox-mark → event replay diproses ulang (bukan di-dedup)
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ProjectionRebuilder>().ResetAsync();
        Assert.Empty(await SnapshotAsync(factory));   // benar-benar kosong pasca reset

        // REPLAY stream event yang SAMA (eventId + occurredAt identik) → rekonstruksi
        await SeedStreamAsync(factory);
        var after = await SnapshotAsync(factory);

        Assert.Equal(before, after);   // projection terkonstruksi ulang IDENTIK (derived data, ADR-0017)
    }

    // ---- harness ----

    // stream event deterministik (eventId + occurredAt FIXED) → seed & replay identik untuk uji rebuild
    private static readonly Guid GrEvent1 = Guid.NewGuid();
    private static readonly Guid GrEvent2 = Guid.NewGuid();
    private static readonly Guid RemovedEvent = Guid.NewGuid();
    private static readonly Guid PutawayEvent = Guid.NewGuid();
    private static readonly Guid PickEvent = Guid.NewGuid();

    private static async Task SeedStreamAsync(WebApplicationFactory<Program> factory)
    {
        await ProjectGrConfirmedAsync(factory, GrEvent1, At, new GRConfirmedV1(
            Guid.NewGuid(), Warehouse, "SUP-1",
            [new ReceivedLineV1("SKU-1", 10, "Good", "B1", null)],
            [new RejectedLineV1("SKU-1", 2, "RejectExcess")]));
        await ProjectGrConfirmedAsync(factory, GrEvent2, At, new GRConfirmedV1(
            Guid.NewGuid(), Warehouse, "SUP-2", [new ReceivedLineV1("SKU-3", 7, "Good", null, null)], []));
        await ProjectStockRemovedAsync(factory, RemovedEvent, At, new StockRemovedV1(
            Guid.NewGuid(), [new StockRemovedLineV1(Warehouse, "SKU-1", "B1", 3)]));
        await ProjectPutawayCompletedAsync(factory, PutawayEvent, At, new PutawayCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-1", Warehouse, "op-1"));
        await ProjectPickingCompletedAsync(factory, PickEvent, At, new PickingCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "B1", 3, "STG-1", "op-1"));
    }

    // snapshot 4 projeksi sebagai list string ter-sortir → bandingkan element-wise (rebuild identik)
    private static async Task<List<string>> SnapshotAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        var lines = new List<string>();
        lines.AddRange((await db.StockOnHandViews.AsNoTracking().ToListAsync())
            .Select(v => $"SOH|{v.WarehouseId}|{v.Sku}|{v.Batch}={v.QtyOnHand}"));
        lines.AddRange((await db.ReceivingSummaries.AsNoTracking().ToListAsync())
            .Select(s => $"RS|{s.SupplierId}|{s.Day}={s.GrCount},{s.ReceivedQty},{s.RejectedQty}"));
        lines.AddRange((await db.DispatchSummaries.AsNoTracking().ToListAsync())
            .Select(d => $"DS|{d.Day}={d.WaveCount},{d.TotalVolume}"));
        lines.AddRange((await db.OperatorActivities.AsNoTracking().ToListAsync())
            .Select(o => $"OA|{o.OperatorId}|{o.Day}={o.PutawayCount},{o.PickCount}"));
        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    private static async Task ProjectGrConfirmedAsync(
        WebApplicationFactory<Program> factory, Guid eventId, DateTimeOffset occurredAt, GRConfirmedV1 message)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<GoodsReceiptConfirmedProjector>().HandleAsync(eventId, occurredAt, message);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
    }

    private static async Task ProjectStockRemovedAsync(
        WebApplicationFactory<Program> factory, Guid eventId, DateTimeOffset occurredAt, StockRemovedV1 message)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<StockRemovedProjector>().HandleAsync(eventId, occurredAt, message);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
    }

    private static async Task ProjectPutawayCompletedAsync(
        WebApplicationFactory<Program> factory, Guid eventId, DateTimeOffset occurredAt, PutawayCompletedV1 message)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<PutawayCompletedProjector>().HandleAsync(eventId, occurredAt, message);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
    }

    private static async Task ProjectPickingCompletedAsync(
        WebApplicationFactory<Program> factory, Guid eventId, DateTimeOffset occurredAt, PickingCompletedV1 message)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<PickingCompletedProjector>().HandleAsync(eventId, occurredAt, message);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
    }

    private async Task<ReportingFactory> CreateFactoryAsync(IUserDirectory? userDirectory = null)
    {
        var factory = new ReportingFactory(await fixture.CreateDatabaseAsync(), userDirectory);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReportingDbContext>().Database.MigrateAsync();
        return factory;
    }

    // WebApplicationFactory host Reporting (Program public partial) — override connection string ke test DB.
    // userDirectory: stub IUserDirectory (ACL) menggantikan adapter gRPC (host AddReportingDirectories) lewat
    // ConfigureTestServices → enrichment-at-read teruji TANPA server Auth nyata (pola stub Notification).
    private sealed class ReportingFactory(string connectionString, IUserDirectory? userDirectory)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:reportingdb", connectionString);
            if (userDirectory is not null)
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IUserDirectory>();
                    services.AddSingleton(userDirectory);
                });
        }
    }

    // stub IUserDirectory — kembalikan username dari peta, null bila tak ada (mensimulasi NotFound → fallback)
    private sealed class StubUserDirectory(IReadOnlyDictionary<string, string> usernames) : IUserDirectory
    {
        public Task<string?> GetUsernameAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(usernames.TryGetValue(userId, out var name) ? name : null);
    }
}
