using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Auth.Application.Features.Logout;

// What: CQRS Command (ADR-0004) + auditable security event (ADR-0033) — logout (revoke refresh token)
// Why: mencabut sesi refresh; IDEMPOTEN (token tak dikenal / sudah tercabut → tetap sukses). AUDITABLE
// (ADR-0033): pencabutan sesi ter-log (timeline forensik). RefreshToken OTOMATIS ter-redaksi; AggregateId
// kosong (token rahasia, korelasi via aktor + traceparent — sama spt RefreshCommand).
public sealed record LogoutCommand(string RefreshToken) : ICommand, IAuditableCommand
{
    public string AggregateType => "RefreshToken";

    public string AggregateId => string.Empty;
}
