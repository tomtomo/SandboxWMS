using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Application.Behaviors;

// What: Pipeline Behavior (MediatR) — Unit-of-Work transaction, command-side
// (ADR-0004 amendment; ADR-0019)
// Why: atomicity write — transaksi bisnis di-commit HANYA saat Result.Success; saat
// Result.Failure ATAU exception → rollback. Ini menutup celah partial-commit yang TIDAK
// tertangkap oleh penanganan-exception saja (ADR-0019: "rollback bukan hanya exception").
// Hanya untuk command (ICommandBase) — query bypass aggregate/repo, jadi tak buka transaksi.
// Posisi paling DALAM (terdaftar terakhir) → membungkus handler langsung.
// How: ICommandBase → BeginTransaction → next() (handler mutasi + SaveChanges enlist ke tx)
// → Commit bila sukses, Rollback bila gagal/throw; query → langsung next() tanpa transaksi.
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICommandBase)
            return await next(cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        TResponse response;
        try
        {
            response = await next(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // optimistic-concurrency conflict (ADR-0031): rollback + map ke Result(Error.Conflict) → 409/
            // Aborted, BUKAN 500. Caller surface 409; operasi idempotent bisa retry/re-read. Menutup
            // RefreshToken rotation-fork (ADR-0016) + lost-update Stock tanpa membocorkan exception EF.
            await transaction.RollbackAsync(cancellationToken);
            return ResultFactory<TResponse>.Failure(
                Error.Conflict("concurrency.conflict", "Sumber daya diubah transaksi lain — coba lagi."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (response.IsSuccess)
            await transaction.CommitAsync(cancellationToken);
        else
            await transaction.RollbackAsync(cancellationToken);

        return response;
    }
}
