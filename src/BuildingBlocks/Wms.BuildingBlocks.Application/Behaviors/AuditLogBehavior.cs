using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Application.Behaviors;

// What: Pipeline Behavior (MediatR) — operational audit log, out-of-band (ADR-0022; ADR-0027)
// Why: tindakan sensitif butuh jejak append-only yang merekam SIAPA melakukan APA pada objek
// mana dengan HASIL apa — TERMASUK attempt yang ditolak. Behavior terdaftar OUTER terhadap
// TransactionBehavior (audit "membungkus" Transaction): saat handler Result.Failure, Transaction
// (di dalam) rollback dulu, lalu behavior ini menulis audit di KONEKSI SENDIRI → baris audit
// SURVIVE rollback bisnis. Itu sebabnya BUKAN Outbox (yang ikut ter-rollback). Hanya
// IAuditableCommand yang di-audit (opt-in eksplisit) — request lain pass-through nol-biaya.
// How: tangkap actor (ICurrentUser, dari scope request selagi HttpContext ada) + payload
// ter-redaksi + traceparent → next() → tulis AuditLogEntry lewat SCOPE BARU (DbContext segar,
// out-of-band) baik pada Result sukses/gagal maupun exception (lalu rethrow). Write best-effort:
// gagal audit di-log, TAK menutupi/menggagalkan hasil bisnis (window non-atomic sadar, ADR-0022).
public sealed class AuditLogBehavior<TRequest, TResponse>(
    ICurrentUser currentUser,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditLogBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IAuditableCommand auditable)
            return await next(cancellationToken);

        // tangkap konteks SEKARANG (scope request) — actor butuh HttpContext yang masih hidup
        var actor = currentUser.UserId;
        var action = request.GetType().Name;
        var payload = AuditRedaction.Redact(request);
        var traceparent = Activity.Current?.Id;

        TResponse response;
        try
        {
            response = await next(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // attempt yang melempar (infra/programmer-error) tetap terekam forensik → lalu rethrow
            await WriteAsync(Build(auditable, actor, action, payload, traceparent,
                isSuccess: false, errorCode: exception.GetType().Name), cancellationToken);
            throw;
        }

        await WriteAsync(Build(auditable, actor, action, payload, traceparent,
            response.IsSuccess, response.IsSuccess ? null : response.Error.Code), cancellationToken);
        return response;
    }

    private static AuditLogEntry Build(
        IAuditableCommand auditable, string actor, string action, string payload, string? traceparent,
        bool isSuccess, string? errorCode) => new()
        {
            Id = Guid.NewGuid(),
            Actor = actor,
            Action = action,
            AggregateType = auditable.AggregateType,
            AggregateId = auditable.AggregateId,
            IsSuccess = isSuccess,
            ErrorCode = errorCode,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow,
            Traceparent = traceparent,
        };

    // What: write out-of-band (ADR-0022) — scope baru → DbContext segar, lepas dari transaksi bisnis
    // How: scope independen (TransactionBehavior di dalam sudah commit/rollback + dispose tx-nya)
    // → IAuditLogStore adapter (Local=tabel audit_log) menulis di koneksi sendiri → survive rollback.
    private async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IAuditLogStore>();
            await store.WriteAsync(entry, cancellationToken);
        }
        catch (Exception exception)
        {
            // best-effort durability: window non-atomic sadar (state commit tapi audit gagal) —
            // di-log, TAK di-rethrow agar tak menutupi/menggagalkan hasil bisnis yang sah.
            logger.LogError(exception,
                "Audit write gagal: {Action} pada {AggregateType}/{AggregateId} (window non-atomic, ADR-0022).",
                entry.Action, entry.AggregateType, entry.AggregateId);
        }
    }
}
