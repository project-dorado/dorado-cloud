using DoradoCloud.Modules.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Tests.Legacy;

/// <summary>Verifies email-sender selection (log by default, SMTP when configured).</summary>
public sealed class EmailSenderRegistrationTests
{
    private static IEmailSender Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddDoradoEmailSender(configuration);
        return services.BuildServiceProvider().GetRequiredService<IEmailSender>();
    }

    [Fact]
    public void Defaults_to_the_logging_sender()
        => Assert.IsType<LoggingEmailSender>(Resolve(new Dictionary<string, string?>()));

    [Fact]
    public void Uses_smtp_when_configured()
        => Assert.IsType<SmtpEmailSender>(Resolve(new Dictionary<string, string?>
        {
            ["Identity:Security:Email:Provider"] = "smtp",
            ["Identity:Security:Email:Smtp:Host"] = "smtp.example.com",
            ["Identity:Security:Email:Smtp:From"] = "no-reply@example.com"
        }));

    [Fact]
    public void Smtp_without_a_host_falls_back_to_logging()
        => Assert.IsType<LoggingEmailSender>(Resolve(new Dictionary<string, string?>
        {
            ["Identity:Security:Email:Provider"] = "smtp"
        }));
}
