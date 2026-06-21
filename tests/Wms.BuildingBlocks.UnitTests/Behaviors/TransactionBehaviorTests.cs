using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Behaviors;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.UnitTests.Behaviors;

// What: unit test TransactionBehavior — rollback-on-Result.Failure (ADR-0019)
// Why: jaminan inti UoW — commit HANYA saat sukses; Result.Failure & exception → rollback;
// query bypass. Diuji terisolasi dengan fake UoW yang mencatat commit/rollback (nol DB).
public sealed class TransactionBehaviorTests
{
    [Fact]
    public async Task Command_success_commits_once()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var behavior = new TransactionBehavior<TestCommand, Result>(unitOfWork);

        var response = await behavior.Handle(
            new TestCommand(), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(1, unitOfWork.Transaction.Commits);
        Assert.Equal(0, unitOfWork.Transaction.Rollbacks);
    }

    [Fact]
    public async Task Command_failure_rolls_back_without_commit()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var behavior = new TransactionBehavior<TestCommand, Result>(unitOfWork);

        var response = await behavior.Handle(
            new TestCommand(),
            _ => Task.FromResult(Result.Failure(Error.Conflict("test.fail", "gagal"))),
            CancellationToken.None);

        Assert.True(response.IsFailure);
        Assert.Equal(0, unitOfWork.Transaction.Commits);
        Assert.Equal(1, unitOfWork.Transaction.Rollbacks);   // rollback-on-Result.Failure (bukan hanya exception)
    }

    [Fact]
    public async Task Command_exception_rolls_back_and_propagates()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var behavior = new TransactionBehavior<TestCommand, Result>(unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new TestCommand(),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None));

        Assert.Equal(0, unitOfWork.Transaction.Commits);
        Assert.Equal(1, unitOfWork.Transaction.Rollbacks);
    }

    [Fact]
    public async Task Query_skips_transaction()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var behavior = new TransactionBehavior<TestQuery, Result>(unitOfWork);

        var response = await behavior.Handle(
            new TestQuery(), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.False(unitOfWork.TransactionStarted);   // bukan ICommandBase → tak buka transaksi
    }

    private sealed record TestCommand : ICommand;

    private sealed record TestQuery;

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public bool TransactionStarted { get; private set; }

        public RecordingTransaction Transaction { get; } = new();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            TransactionStarted = true;
            return Task.FromResult<ITransaction>(Transaction);
        }
    }

    private sealed class RecordingTransaction : ITransaction
    {
        public int Commits { get; private set; }

        public int Rollbacks { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commits++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollbacks++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
