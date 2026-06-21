using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Platform.Local.Messaging;

namespace Wms.Inbound.IntegrationTests;

// What: integration test rail Outbox/Inbox (DoD Phase 01b) atas Postgres nyata
// Why: membuktikan jaminan inti — round-trip publish→dispatch→consume, idempotency
// at-least-once (duplikat ditekan), dan Dead Letter Channel untuk poison message.
[Collection(PostgresCollection.Name)]
public sealed class OutboxInboxRailTests(PostgresFixture fixture)
{
    private const string ConsumerHandlerType = "test-consumer";
    private const string LogicalName = "inbound.gr_confirmed.v1";

    [Fact]
    public async Task Round_trip_dispatches_outbox_and_consumer_handles_event_once()
    {
        var options = await CreateSchemaAsync();

        var invocations = 0;
        var publisher = SubscribeConsumer(options, () => Interlocked.Increment(ref invocations));

        await EnqueueAsync(options, NewEnvelope());
        await DispatchOnceAsync(options, publisher);

        await using var db = new RailTestDbContext(options);
        Assert.Equal(1, await db.Set<HandledEvent>().CountAsync());   // efek bisnis sekali
        Assert.Equal(1, await db.Set<InboxMessage>().CountAsync());   // inbox 1 baris
        Assert.Equal(1, invocations);                                 // konsumer terpanggil sekali
        var outbox = await db.Set<OutboxMessage>().SingleAsync();
        Assert.NotNull(outbox.ProcessedAt);                           // outbox ter-dispatch
    }

    [Fact]
    public async Task Duplicate_delivery_is_suppressed_by_inbox()
    {
        var options = await CreateSchemaAsync();

        var invocations = 0;
        var publisher = SubscribeConsumer(options, () => Interlocked.Increment(ref invocations));

        var envelope = NewEnvelope();

        // event yang SAMA dikirim dua kali (mis. redelivery broker / outbox retry)
        await publisher.PublishAsync(envelope);
        await publisher.PublishAsync(envelope);

        await using var db = new RailTestDbContext(options);
        Assert.Equal(2, invocations);                                // handler jalan dua kali…
        Assert.Equal(1, await db.Set<HandledEvent>().CountAsync());  // …tapi efek hanya sekali
        Assert.Equal(1, await db.Set<InboxMessage>().CountAsync());  // inbox tetap 1 baris
    }

    [Fact]
    public async Task Poison_message_is_dead_lettered_after_max_attempts()
    {
        var options = await CreateSchemaAsync();
        var publisher = new ThrowingPublisher();
        var dispatcher = NewDispatcher(maxAttempts: 3);

        await EnqueueAsync(options, NewEnvelope());

        // tiga pass gagal publish → attempt mencapai batas → dead-letter
        for (var pass = 0; pass < 3; pass++)
        {
            await using var db = new RailTestDbContext(options);
            await dispatcher.ProcessOnceAsync(db, publisher, new LocalDeadLetterStore(db));
        }

        await using var assert = new RailTestDbContext(options);
        Assert.Equal(1, await assert.Set<DeadLetterMessage>().CountAsync());
        var outbox = await assert.Set<OutboxMessage>().SingleAsync();
        Assert.Equal(3, outbox.Attempts);
        Assert.NotNull(outbox.ProcessedAt);                          // berhenti retry (tak memblok lain)
    }

    // --- harness helpers ---

    private async Task<DbContextOptions<RailTestDbContext>> CreateSchemaAsync()
    {
        var options = OptionsFor(await fixture.CreateDatabaseAsync());
        await using var db = new RailTestDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return options;
    }

    private static DbContextOptions<RailTestDbContext> OptionsFor(string connectionString) =>
        new DbContextOptionsBuilder<RailTestDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

    private static MessageEnvelope NewEnvelope() =>
        new(Guid.NewGuid(), LogicalName, DateTimeOffset.UtcNow, """{"grId":"GR-1"}""", null, null);

    private static async Task EnqueueAsync(
        DbContextOptions<RailTestDbContext> options, MessageEnvelope envelope)
    {
        // produser: tulis integration event ke outbox dalam "transaksi bisnis"
        await using var db = new RailTestDbContext(options);
        db.AddToOutbox(envelope);
        await db.SaveChangesAsync();
    }

    private static async Task DispatchOnceAsync(
        DbContextOptions<RailTestDbContext> options, IMessagePublisher publisher)
    {
        await using var db = new RailTestDbContext(options);
        await NewDispatcher().ProcessOnceAsync(db, publisher, new LocalDeadLetterStore(db));
    }

    // konsumer in-proc: inbox-dedup + business write dalam SATU SaveChanges (atomic)
    private static InMemoryMessagePublisher SubscribeConsumer(
        DbContextOptions<RailTestDbContext> options, Action onInvoke)
    {
        var publisher = new InMemoryMessagePublisher(NullLogger<InMemoryMessagePublisher>.Instance);
        publisher.Subscribe(async (envelope, ct) =>
        {
            onInvoke();
            await using var db = new RailTestDbContext(options);
            if (await db.HasProcessedAsync(envelope.EventId, ConsumerHandlerType, ct))
                return;

            db.Set<HandledEvent>().Add(new HandledEvent
            {
                Id = Guid.NewGuid(),
                EventId = envelope.EventId,
                LogicalName = envelope.LogicalName,
            });
            db.MarkProcessed(envelope.EventId, ConsumerHandlerType, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
        });
        return publisher;
    }

    private static OutboxDispatcher NewDispatcher(int maxAttempts = 5) =>
        new(new UnusedScopeFactory(),
            new OutboxOptions { MaxAttempts = maxAttempts },
            NullLogger<OutboxDispatcher>.Instance);

    // ProcessOnceAsync tak menyentuh scope factory (itu hanya untuk loop BackgroundService)
    private sealed class UnusedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException();
    }

    // simulasi broker down → memicu jalur retry/dead-letter dispatcher
    private sealed class ThrowingPublisher : IMessagePublisher
    {
        public Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("broker unavailable (simulated).");
    }
}
