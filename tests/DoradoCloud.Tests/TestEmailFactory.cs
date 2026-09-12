using System.Collections.Concurrent;
using DoradoCloud.Modules.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DoradoCloud.Tests;

/// <summary>A captured outbound email.</summary>
public sealed record SentEmail(string To, string Subject, string Body);

/// <summary>
/// In-memory <see cref="IEmailSender"/> sink: captures messages so tests can
/// follow emailed verification/reset links without an SMTP provider.
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => _sent.ToArray();

    public SentEmail? LastTo(string to)
        => _sent.ToArray().LastOrDefault(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase));

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(new SentEmail(to, subject, htmlBody));
        return Task.CompletedTask;
    }
}

/// <summary>Boots the API with the logging sender replaced by an in-memory sink.</summary>
public class EmailCapturingFactory : CloudApiFactory
{
    public RecordingEmailSender Sender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Sender);
        });
    }
}

/// <summary>The email-capturing host with <c>RequireEmailVerification</c> enabled.</summary>
public sealed class RequireEmailVerificationFactory : EmailCapturingFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Identity:Security:RequireEmailVerification", "true");
    }
}
