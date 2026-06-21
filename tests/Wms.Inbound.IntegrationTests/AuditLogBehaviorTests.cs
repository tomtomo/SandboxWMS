using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.DependencyInjection;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Domain.Results;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Inbound.IntegrationTests;

// What: behavioral test AuditLogBehavior (DoD Phase 02c; ADR-0022) atas Postgres nyata
// Why: membuktikan jaminan inti audit out-of-band — command teraudit yang Result.Failure
// memicu rollback transaksi BISNIS, TAPI baris audit_log TETAP tertulis (koneksi sendiri,
// survive rollback). Itu sebabnya audit BUKAN via Outbox. Control: command sukses → audit
// IsSuccess=true + efek bisnis ter-commit. Real Postgres (Testcontainers) via PostgresFixture.
[Collection(PostgresCollection.Name)]
public sealed class AuditLogBehaviorTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Audited_command_writes_audit_even_when_business_transaction_rolls_back()
    {
        await using var provider = await BuildPipelineAsync();
        var entityId = Guid.NewGuid();

        Result result;
        using (var scope = provider.CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new FailingAuditedCommand(entityId));

        Assert.True(result.IsFailure);
        Assert.Equal("test.rejected", result.Error.Code);

        using var assert = provider.CreateScope();
        var db = assert.ServiceProvider.GetRequiredService<RailTestDbContext>();

        Assert.Equal(0, await db.Set<HandledEvent>().CountAsync());            // bisnis ROLLBACK
        var audit = Assert.Single(await db.Set<AuditLogEntry>().ToListAsync()); // audit TETAP tertulis
        Assert.False(audit.IsSuccess);                                         // outcome-aware: ditolak
        Assert.Equal("test.rejected", audit.ErrorCode);                       // Error.Code dari Result
        Assert.Equal(SystemActor.Id, audit.Actor);                            // actor dari ICurrentUser
        Assert.Equal("TestAggregate", audit.AggregateType);                   // dari IAuditableCommand
        Assert.Equal(entityId.ToString(), audit.AggregateId);
        Assert.Equal(nameof(FailingAuditedCommand), audit.Action);
    }

    [Fact]
    public async Task Audited_command_success_writes_audit_and_commits_business_effect()
    {
        await using var provider = await BuildPipelineAsync();
        var entityId = Guid.NewGuid();

        Result result;
        using (var scope = provider.CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new SucceedingAuditedCommand(entityId));

        Assert.True(result.IsSuccess);

        using var assert = provider.CreateScope();
        var db = assert.ServiceProvider.GetRequiredService<RailTestDbContext>();

        Assert.Equal(1, await db.Set<HandledEvent>().CountAsync());            // bisnis COMMIT
        var audit = Assert.Single(await db.Set<AuditLogEntry>().ToListAsync());
        Assert.True(audit.IsSuccess);                                          // outcome-aware: sukses
        Assert.Null(audit.ErrorCode);
    }

    // pipeline mirror produksi: MediatR + AddBuildingBlocksBehaviors (Logging→…→AuditLog→Transaction)
    // atas RailTestDbContext (memetakan audit_log + handled_event via AddInfrastructureTables).
    private async Task<ServiceProvider> BuildPipelineAsync()
    {
        var connection = await fixture.CreateDatabaseAsync();
        var provider = new ServiceCollection()
            .AddLogging()
            .AddDbContext<RailTestDbContext>(options => options
                .UseNpgsql(connection)
                .UseSnakeCaseNamingConvention())
            .AddScoped<DbContext>(sp => sp.GetRequiredService<RailTestDbContext>())
            .AddScoped<ICurrentUser, SystemCurrentUser>()   // actor mesin (test origin)
            .AddTransactionalMessaging()                    // IUnitOfWork (EfUnitOfWork)
            .AddLocalAuditing()                             // IAuditLogStore (LocalAuditLogStore)
            .AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(AuditLogBehaviorTests).Assembly);
                cfg.AddBuildingBlocksBehaviors();
            })
            .BuildServiceProvider();

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<RailTestDbContext>().Database.EnsureCreatedAsync();
        return provider;
    }
}

// What: synthetic auditable command yang MUTASI lalu GAGAL — bukti audit survive rollback
public sealed record FailingAuditedCommand(Guid EntityId) : ICommand, IAuditableCommand
{
    public string AggregateType => "TestAggregate";

    public string AggregateId => EntityId.ToString();
}

public sealed class FailingAuditedHandler(DbContext db, IUnitOfWork unitOfWork)
    : IRequestHandler<FailingAuditedCommand, Result>
{
    public async Task<Result> Handle(FailingAuditedCommand command, CancellationToken cancellationToken)
    {
        // efek bisnis ditulis DI DALAM transaksi (TransactionBehavior) → akan di-rollback karena Failure
        db.Set<HandledEvent>().Add(new HandledEvent
        {
            Id = command.EntityId,
            EventId = command.EntityId,
            LogicalName = "test.failing",
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Failure(Error.Conflict("test.rejected", "ditolak untuk membuktikan rollback."));
    }
}

// What: synthetic auditable command yang MUTASI lalu SUKSES — control happy-path
public sealed record SucceedingAuditedCommand(Guid EntityId) : ICommand, IAuditableCommand
{
    public string AggregateType => "TestAggregate";

    public string AggregateId => EntityId.ToString();
}

public sealed class SucceedingAuditedHandler(DbContext db, IUnitOfWork unitOfWork)
    : IRequestHandler<SucceedingAuditedCommand, Result>
{
    public async Task<Result> Handle(SucceedingAuditedCommand command, CancellationToken cancellationToken)
    {
        db.Set<HandledEvent>().Add(new HandledEvent
        {
            Id = command.EntityId,
            EventId = command.EntityId,
            LogicalName = "test.success",
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
