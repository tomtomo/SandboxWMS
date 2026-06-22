using Wms.Auth.Application.Security;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Auth.Application.Features.Login;

// What: CQRS Command (ADR-0004) — login (verify kredensial → terbitkan access + refresh)
// Why: write-intent menghasilkan Result<AuthTokens> sebagai NILAI (no-throw, ADR-0019). Tak auditable:
// pelaku belum terotentikasi saat login (pre-auth) → audit createdBy login = anonymous by design.
public sealed record LoginCommand(string Username, string Password) : ICommand<AuthTokens>;
