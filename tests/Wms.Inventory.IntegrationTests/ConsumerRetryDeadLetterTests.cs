using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inbound.Contracts;
using Wms.Inventory.Application.Features.ConsumeGoodsReceiptConfirmed;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Messaging;
using Wms.Inventory.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Inventory.IntegrationTests;

// What: integration test retry → Dead Letter Channel baseline (EIP; ADR-0005/0010, Phase 02b)
// Why: membuktikan ConsumerDeadLetterPipeline membungkus consumer Inventory REAL — pesan racun
// (GRConfirmedV1 dgn line invalid) yang gagal handle berulang mendarat di tabel dead_letter
// setelah retry habis, BUKAN hilang diam-diam; sebaliknya event valid lewat tanpa dead-letter
// (pipeline transparan saat sukses). Real Postgres (Testcontainers) via PostgresFixture.
[Collection(PostgresCollection.Name)]
public sealed class ConsumerRetryDeadLetterTests(PostgresFixture fixture)
{
    private const int MaxAttempts = 3;

    [Fact]
    public async Task Poison_gr_confirmed_lands_in_dead_letter_after_retries_exhausted()
    {
        await using var inventory = await BuildInventoryAsync();
        var handler = WrapDispatcher(inventory);

        // poison: line SKU kosong → Stock.CreateOnHand gagal (MissingSku) di SETIAP attempt →
        // dispatcher throw → pipeline retry sampai habis → dead_letter.
        var poison = GRConfirmedEnvelope("WH-JKT", ("", 10));
        await handler(poison, CancellationToken.None); // pipeline swallow setelah DLQ — tak throw

        using var scope = inventory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var deadLetters = await db.Set<DeadLetterMessage>().ToListAsync();
        var deadLetter = Assert.Single(deadLetters);
        Assert.Equal(poison.EventId, deadLetter.EventId);                       // korelasi forensik
        Assert.Equal(GRConfirmedV1.LogicalName, deadLetter.LogicalName);
        Assert.Equal(GoodsReceiptConfirmedConsumer.HandlerType, deadLetter.Source);
        Assert.Equal(MaxAttempts, deadLetter.Attempts);                         // retry benar-benar habis

        Assert.Equal(0, await db.Stocks.CountAsync());                          // tak ada efek parsial
        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());             // tak pernah mark processed
    }

    [Fact]
    public async Task Valid_gr_confirmed_through_pipeline_succeeds_without_dead_letter()
    {
        await using var inventory = await BuildInventoryAsync();
        var handler = WrapDispatcher(inventory);

        var valid = GRConfirmedEnvelope("WH-JKT", ("SKU-1", 10));
        await handler(valid, CancellationToken.None);

        using var scope = inventory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        Assert.Empty(await db.Set<DeadLetterMessage>().ToListAsync());          // sukses → tak ada DLQ
        Assert.Equal(1, await db.PutawayTasks.CountAsync());
        Assert.Equal(1, await db.Set<InboxMessage>().CountAsync());            // diproses sekali (Inbox)

        // SYSTEM actor (ADR-0027) + IAuditable interceptor: consumer = origin-mesin (tanpa HttpContext)
        // → created_by ter-stempel SYSTEM saat Stock di-persist. Bukti template audit lengkap end-to-end.
        var stock = await db.Stocks.SingleAsync();                              // kerja consumer tetap jalan
        Assert.Equal(SystemActor.Id, stock.CreatedBy);
    }

    // inventory provider mirror host: infrastruktur modul + adapter Local (IDeadLetterStore).
    private async Task<ServiceProvider> BuildInventoryAsync()
    {
        var inventory = new ServiceCollection()
            .AddLogging()
            .AddInventoryInfrastructure(await fixture.CreateDatabaseAsync())
            .AddLocalMessaging()
            .BuildServiceProvider();

        using var scope = inventory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.EnsureCreatedAsync();
        return inventory;
    }

    // bungkus dispatcher Inventory REAL dengan pipeline retry→DLQ (RetryDelay 0 = test cepat).
    private static Func<MessageEnvelope, CancellationToken, Task> WrapDispatcher(ServiceProvider inventory)
    {
        var pipeline = new ConsumerDeadLetterPipeline(
            inventory.GetRequiredService<IServiceScopeFactory>(),
            new ConsumerRetryOptions { MaxAttempts = MaxAttempts, RetryDelay = TimeSpan.Zero },
            NullLogger<ConsumerDeadLetterPipeline>.Instance);
        var dispatcher = inventory.GetRequiredService<InventoryIntegrationEventDispatcher>();
        return pipeline.Wrap(
            GoodsReceiptConfirmedConsumer.HandlerType, dispatcher.HandleGoodsReceiptConfirmedAsync);
    }

    private static MessageEnvelope GRConfirmedEnvelope(string warehouseId, params (string Sku, int Qty)[] lines)
    {
        var payload = new GRConfirmedV1(
            Guid.NewGuid(), warehouseId,
            [.. lines.Select(line => new ReceivedLineV1(line.Sku, line.Qty, "Good", null, null))],
            []);

        return new MessageEnvelope(
            EventId: Guid.NewGuid(),
            LogicalName: GRConfirmedV1.LogicalName,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: JsonSerializer.Serialize(payload),
            Traceparent: null,
            Tracestate: null);
    }
}
