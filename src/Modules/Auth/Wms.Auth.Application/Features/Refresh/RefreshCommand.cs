using Wms.Auth.Application.Security;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Auth.Application.Features.Refresh;

// What: CQRS Command (ADR-0004) — refresh (rotate token → access + refresh baru)
// Why: re-issue access JWT tanpa login ulang; menghasilkan Result<AuthTokens>. RawRefreshToken = token
// mentah yang disajikan client (server hash lalu lookup, ADR-0016).
public sealed record RefreshCommand(string RefreshToken) : ICommand<AuthTokens>;
