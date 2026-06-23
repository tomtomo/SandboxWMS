using Wms.Auth.Application.Security;
using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Auth.Application.Features.Refresh;

// What: CQRS Command (ADR-0004) + auditable security event (ADR-0033) — refresh (rotate token → access + refresh)
// Why: re-issue access JWT tanpa login ulang; menghasilkan Result<AuthTokens>. RefreshToken = token mentah yang
// disajikan client (server hash lalu lookup, ADR-0016). AUDITABLE (ADR-0033): tiap refresh ter-log; REUSE-
// DETECTION cascade (token tercabut disajikan ulang) → Failure(not_active) tetap ter-audit out-of-band = sinyal
// kompromi. RefreshToken OTOMATIS ter-redaksi. AggregateId kosong: token rahasia (tak boleh ke kolom tak-ter-
// redaksi), korelasi via aktor + traceparent + timeline RevokedAt (ADR-0033).
public sealed record RefreshCommand(string RefreshToken) : ICommand<AuthTokens>, IAuditableCommand
{
    public string AggregateType => "RefreshToken";

    public string AggregateId => string.Empty;
}
