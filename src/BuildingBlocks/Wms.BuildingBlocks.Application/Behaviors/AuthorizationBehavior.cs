using MediatR;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Application.Behaviors;

// What: Pipeline Behavior (MediatR) — authorization slot, DEFERRED (ADR-0004 amendment; ADR-0012)
// Why: posisi authz dikunci di pipeline (setelah Logging, sebelum Validation/Transaction)
// supaya kelak fail-fast SEBELUM transaksi dibuka — tapi enforcement-nya ditunda ke Phase 07a
// (deferred authorization, ADR-0012). Slot di-reserve sekarang agar urutan pipeline tak
// bergeser saat enforcement di-wire.
// How: pass-through murni; aktivasi nanti = mengisi cek permission DI SINI, bukan menyisipkan
// behavior baru ke tengah urutan.
public sealed class AuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // TODO-AUTH: pipeline-authz — cek [Authorize(Permission)] di sini (Phase 07a, ADR-0012)
        return next(cancellationToken);
    }
}
