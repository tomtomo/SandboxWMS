using Grpc.Core;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Domain;
using Wms.Auth.Grpc;
using Wms.BuildingBlocks.Web.ErrorHandling;

namespace Wms.Auth.Api.Grpc;

// What: gRPC Service impl read-API Auth (ADR-0006/0011) — reader-delegation
// Why: implement service base hasil codegen (*.Grpc) dengan DELEGASI ke IAuthReader (cache-aside decorator)
// — TIDAK inject DbContext (dijaga FF#8: gRPC `.Api` bebas EF). Not-found → ResultFailureException(NotFound)
// dipetakan ResultExceptionInterceptor (server) ke RpcException(StatusCode.NotFound) — mapping tak tersebar
// (ADR-0019). GetUser = claim-source: read-model sudah filter role aktif (IsActive, ADR-0012).
public sealed class AuthReadService(IAuthReader reader) : AuthReadApi.AuthReadApiBase
{
    public override async Task<UserReply> GetUser(GetUserRequest request, ServerCallContext context)
    {
        // malformed id → Guid.Empty → reader null → NotFound (tak bocorkan detail parsing)
        Guid.TryParse(request.UserId, out var id);
        var user = await reader.GetUserAsync(id, context.CancellationToken);
        if (user is null)
            throw new ResultFailureException(UserErrors.NotFound);

        var reply = new UserReply
        {
            UserId = user.UserId.ToString(),
            Username = user.Username,
            Email = user.Email,
            Status = user.Status,
        };
        reply.RoleCodes.AddRange(user.RoleCodes);
        reply.PermissionCodes.AddRange(user.PermissionCodes);
        reply.WarehouseIds.AddRange(user.WarehouseIds.Select(warehouseId => warehouseId.ToString()));
        return reply;
    }

    public override async Task<RoleReply> GetRole(GetRoleRequest request, ServerCallContext context)
    {
        Guid.TryParse(request.RoleId, out var id);
        var role = await reader.GetRoleAsync(id, context.CancellationToken);
        if (role is null)
            throw new ResultFailureException(RoleErrors.NotFound);

        var reply = new RoleReply
        {
            RoleId = role.RoleId.ToString(),
            Code = role.Code,
            Name = role.Name,
        };
        reply.PermissionCodes.AddRange(role.PermissionCodes);
        return reply;
    }

    public override async Task<PermissionReply> GetPermission(GetPermissionRequest request, ServerCallContext context)
    {
        var permission = await reader.GetPermissionAsync(request.Code, context.CancellationToken);
        if (permission is null)
            throw new ResultFailureException(PermissionErrors.NotFound);

        return new PermissionReply
        {
            Code = permission.Code,
            Description = permission.Description,
        };
    }
}
