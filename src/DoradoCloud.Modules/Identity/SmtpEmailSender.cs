using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Sends account emails over SMTP (<c>Identity:Security:Email:Provider=smtp</c>).
/// Used only when the provider is selected and <see cref="SmtpOptions.IsConfigured"/>;
/// otherwise the app falls back to <see cref="LoggingEmailSender"/>. Failures are
/// logged and swallowed so a mail outage cannot break sign-up or password reset.
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<IdentitySecurityOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var smtp = options.Value.Email.Smtp;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(smtp.From, smtp.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.UseStartTls,
                Credentials = string.IsNullOrWhiteSpace(smtp.User)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(smtp.User, smtp.Password)
            };

            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Sent account email to {To} via {Host}.", to, smtp.Host);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            logger.LogError(ex, "Failed to send account email to {To} via {Host}.", to, smtp.Host);
        }
    }
}
