using Microsoft.Extensions.Logging;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Outbound email abstraction (M11). The default implementation logs the message
/// so email verification and password reset work without a provider; operators
/// can replace it with an SMTP/provider sender.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IEmailSender"/>: records the message to the log.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Outbound email to {To}: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
