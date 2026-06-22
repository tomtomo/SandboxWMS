using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Auth.Application.Features.Logout;

// What: CQRS Command (ADR-0004) — logout (revoke refresh token), tanpa nilai balik
// Why: mencabut sesi refresh; IDEMPOTEN (token tak dikenal / sudah tercabut → tetap sukses).
public sealed record LogoutCommand(string RefreshToken) : ICommand;
