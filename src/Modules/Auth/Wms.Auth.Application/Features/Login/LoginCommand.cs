using Wms.Auth.Application.Security;
using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Auth.Application.Features.Login;

// What: CQRS Command (ADR-0004) + auditable security event (ADR-0033) — login (verify kredensial → access + refresh)
// Why: write-intent menghasilkan Result<AuthTokens> sebagai NILAI (no-throw, ADR-0019). AUDITABLE (ADR-0033):
// login attempt (sukses/gagal/lockout) = security event yang WAJIB ter-log (OWASP ASVS V7) — aktor anonymous
// saat pre-auth itu FAKTA yang dicatat, bukan alasan opt-out. Password OTOMATIS ter-redaksi (AuditRedaction).
public sealed record LoginCommand(string Username, string Password) : ICommand<AuthTokens>, IAuditableCommand
{
    public string AggregateType => "User";

    public string AggregateId => Username;
}
