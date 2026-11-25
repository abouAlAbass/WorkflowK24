using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// Production SMTP email adapter consuming dynamic configuration from SystemSettings
/// with support for STARTTLS/SSL, credentials, pickup directory fallback, and safe dry-run mode.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly ISystemSettingRepository _settingRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        ISystemSettingRepository settingRepository,
        IConfiguration configuration,
        ILogger<SmtpEmailService> logger)
    {
        _settingRepository = settingRepository ?? throw new ArgumentNullException(nameof(settingRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentException("Recipient email address ('to') cannot be empty.", nameof(to));
        }

        var settingsList = await _settingRepository.GetAllAsync(ct);
        var settings = settingsList.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        string smtpHost = settings.GetValueOrDefault("smtp.server")
            ?? _configuration["Email:SmtpHost"]
            ?? "localhost";

        int smtpPort = int.TryParse(
            settings.GetValueOrDefault("smtp.port") ?? _configuration["Email:SmtpPort"],
            out var parsedPort) && parsedPort > 0
            ? parsedPort
            : 587;

        string security = settings.GetValueOrDefault("smtp.security")
            ?? _configuration["Email:SmtpSecurity"]
            ?? "STARTTLS";

        string fromAddress = settings.GetValueOrDefault("smtp.from_address")
            ?? _configuration["Email:FromAddress"]
            ?? "noreply@tamam-workflow.internal";

        string? username = settings.GetValueOrDefault("smtp.username") ?? _configuration["Email:Username"];
        string? password = settings.GetValueOrDefault("smtp.password") ?? _configuration["Email:Password"];
        string? pickupDirectory = _configuration["Email:PickupDirectory"];

        bool liveSmtpEnabled =
            (bool.TryParse(settings.GetValueOrDefault("smtp.enabled"), out var dbEnabled) && dbEnabled) ||
            (bool.TryParse(_configuration["Email:UseLiveSmtp"], out var cfgEnabled) && cfgEnabled);

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, "Tamam Workflow Studio"),
            Subject = subject ?? string.Empty,
            Body = body ?? string.Empty,
            IsBodyHtml = isHtml
        };

        foreach (var recipient in to.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            message.To.Add(recipient);
        }

        if (!string.IsNullOrWhiteSpace(pickupDirectory))
        {
            Directory.CreateDirectory(pickupDirectory);
            using var pickupClient = new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = pickupDirectory
            };
            await pickupClient.SendMailAsync(message, ct);
            _logger.LogInformation("Email written to pickup directory '{PickupDir}' for {To} | Subject: {Subject}", pickupDirectory, to, subject);
            return;
        }

        if (!liveSmtpEnabled)
        {
            _logger.LogInformation(
                "SMTP dry-run delivery (set 'smtp.enabled=true' or 'Email:UseLiveSmtp=true' for live relay via {Host}:{Port} [{Security}]). To: {To} | Subject: {Subject}",
                smtpHost, smtpPort, security, to, subject);
            return;
        }

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = !string.Equals(security, "NONE", StringComparison.OrdinalIgnoreCase),
            Timeout = 15_000
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(username, password ?? string.Empty);
        }

        await client.SendMailAsync(message, ct);

        _logger.LogInformation(
            "Live SMTP email dispatched via {Host}:{Port}. To: {To}, Subject: {Subject}",
            smtpHost, smtpPort, to, subject);
    }
}
