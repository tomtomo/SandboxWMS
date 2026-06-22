using Wms.BuildingBlocks.Application.Notification;
using Wms.Notification.Directory;

namespace Wms.Notification.IntegrationTests;

// What: test double IUserDirectory (ACL Auth read-API) — recipient detail tanpa gRPC nyata
// Why: integration test tak menjalankan host Auth; stub mengembalikan email deterministik agar worker
// bisa dispatch channel Email. DoD read-API (resolve recipient) di-buktikan via adapter REAL nanti (05d).
internal sealed class FakeUserDirectory : IUserDirectory
{
    public Task<UserContact?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<UserContact?>(new UserContact(userId, $"user-{userId}", $"{userId}@example.test"));
}

// What: test double IWarehouseDirectory (ACL MasterData read-API) — warehouse context tanpa gRPC nyata
internal sealed class FakeWarehouseDirectory : IWarehouseDirectory
{
    public Task<WarehouseContext?> GetWarehouseAsync(string warehouseId, CancellationToken cancellationToken = default)
        => Task.FromResult<WarehouseContext?>(new WarehouseContext(warehouseId, $"Warehouse {warehouseId}"));
}

// What: test double IEmailSender — controllable channel (record kirim / inject kegagalan)
// Why: happy-path → record + sukses; failed→retry→DLQ → ShouldFail=true (lempar) untuk membuktikan worker
// retry s/d max lalu Dead Letter Channel. Local LoggingEmailSender selalu sukses → tak bisa uji jalur gagal.
internal sealed class RecordingEmailSender : IEmailSender
{
    public bool ShouldFail { get; init; }

    public List<(string Email, string Subject, string Body)> Sent { get; } = [];

    public Task<string> SendAsync(
        string emailAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
            throw new InvalidOperationException("simulasi kegagalan channel email (provider down).");

        Sent.Add((emailAddress, subject, body));
        return Task.FromResult($"fake-email-{Guid.NewGuid():N}");
    }
}
