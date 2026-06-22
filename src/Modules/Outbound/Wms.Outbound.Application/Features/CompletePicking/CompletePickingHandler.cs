using System.Text.Json;
using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Contracts;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Features.CompletePicking;

// What: CQRS — Command Handler (MediatR) + domain→integration event translation (ADR-0005/0028) — §C5
// Why: use-case mengoordinasi DUA aggregate dalam SATU transaksi — PickingTask Assigned→Completed (raise
// PickingCompleted, single-aggregate fact) DAN gate agregasi Wave→Ready saat SEMUA task wave Completed.
// PickingCompleted (domain) diterjemahkan ke PickingCompletedV1 (published language) → Outbox: Inventory
// transisi Stock Allocated→Picked (ADR-0028). Gate Wave→Ready ditegakkan DOMAIN (wave.MarkReady); handler
// hanya menyuplai himpunan task Completed (di-query) — NotAllPicked = belum siap (no-op), bukan error.
// How: load task → Complete(staging) → translate event → Enqueue Outbox → ClearDomainEvents. Load semua
// PickingTask wave → completedIds → wave.MarkReady(completedIds) (swallow NotAllPicked). SaveChanges atomic.
public sealed class CompletePickingHandler(
    IPickingTaskRepository pickingTaskRepository,
    IWaveRepository waveRepository,
    IIntegrationEventOutbox outbox,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CompletePickingCommand, Result>
{
    public async Task<Result> Handle(CompletePickingCommand command, CancellationToken cancellationToken)
    {
        var task = await pickingTaskRepository.GetByIdAsync(
            new PickingTaskId(command.PickingTaskId), cancellationToken);
        if (task is null)
            return Result.Failure(PickingTaskErrors.NotFound);

        var complete = task.Complete(command.StagingLocationId);
        if (complete.IsFailure)
            return complete;

        // operatorId = aktor penyelesai pick (ICurrentUser, ADR-0030) — di-source di sini, BUKAN di domain
        // event (PickingCompleted nol-aktor). Origin-mesin/authZ-deferred → SYSTEM (ADR-0027) sampai 07a.
        foreach (var completed in task.DomainEvents.OfType<PickingCompleted>())
            outbox.Enqueue(ToEnvelope(completed, currentUser.UserId));
        task.ClearDomainEvents();

        // gate Wave→Ready (overview §C5): wave siap saat SEMUA PickingTask-nya Completed. Domain (MarkReady)
        // pemegang aturan; handler menyuplai fakta completion dari query (task yang baru selesai sudah tracked).
        var wave = await waveRepository.GetByIdAsync(new WaveId(task.WaveId), cancellationToken);
        if (wave is null)
            return Result.Failure(WaveErrors.NotFound);

        var waveTasks = await pickingTaskRepository.ListByWaveAsync(task.WaveId, cancellationToken);
        var completedIds = waveTasks
            .Where(entry => entry.Status == PickingTaskStatus.Completed)
            .Select(entry => entry.Id.Value)
            .ToList();

        var ready = wave.MarkReady(completedIds);
        if (ready.IsFailure && ready.Error != WaveErrors.NotAllPicked)
            return ready; // NotAllPicked = belum semua task selesai → wave tetap Active (no-op)

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // What: Message Translator (EIP) — domain event → integration event envelope (ADR-0005/0028/0030)
    // How: PickingCompleted (domain) → PickingCompletedV1 (contract) + operatorId (aktor, dari handler);
    // EventId baru = identitas outbox/idempotency (Inventory dedup via Inbox → Stock Allocated→Picked;
    // Reporting → OperatorActivity pick-count per operator).
    private static MessageEnvelope ToEnvelope(PickingCompleted completed, string operatorId)
    {
        var payload = new PickingCompletedV1(
            completed.WaveId,
            completed.PickingTaskId.Value,
            completed.StockId,
            completed.Sku,
            completed.Batch,
            completed.Qty,
            completed.StagingLocationId,
            operatorId);

        return new MessageEnvelope(
            EventId: Guid.NewGuid(),
            LogicalName: PickingCompletedV1.LogicalName,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: JsonSerializer.Serialize(payload),
            Traceparent: null,
            Tracestate: null);
    }
}
