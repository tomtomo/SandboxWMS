using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Application.Notification;

namespace Wms.Platform.Local.Notification;

// What: Adapter Local untuk port IEmailSender (log stub, ADR-0017 channel abstraction)
// Why: environment lokal tak punya SMTP — "kirim" = catat ke log + kembalikan synthetic
// providerMessageId. Adapter cloud (SendGrid/ACS) menggantikannya tanpa sentuh worker
// (Hexagonal). Branded provider di-defer (out-of-scope 04d).
// How: log informational lalu return id deterministik-cukup (prefix + Guid) sebagai bukti kirim.
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task<string> SendAsync(
        string emailAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[email] to={EmailAddress} subject={Subject} body={Body}", emailAddress, subject, body);
        return Task.FromResult($"local-email-{Guid.NewGuid():N}");
    }
}
