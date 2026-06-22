namespace Wms.Auth.Application.ReadModels;

// What: read-DTO untuk gRPC read-API (CQRS read-side, ADR-0011) — bypass aggregate
// Why: konsumen (mis. Notification 04d → recipient detail) baca User/Role/Permission via read-API
// sinkron. RoleCodes/PermissionCodes hanya dari role AKTIF (IsActive filter di reader, ADR-0012 —
// claim-source gRPC tak boleh bocorkan permission role non-aktif). Status di-expose agar konsumen tahu
// kelayakan user. Pure data bag — nol behavior.
public sealed record UserReadModel(
    Guid UserId,
    string Username,
    string Email,
    string Status,
    IReadOnlyCollection<string> RoleCodes,
    IReadOnlyCollection<string> PermissionCodes,
    IReadOnlyCollection<Guid> WarehouseIds);

public sealed record RoleReadModel(
    Guid RoleId,
    string Code,
    string Name,
    IReadOnlyCollection<string> PermissionCodes);

public sealed record PermissionReadModel(string Code, string Description);
