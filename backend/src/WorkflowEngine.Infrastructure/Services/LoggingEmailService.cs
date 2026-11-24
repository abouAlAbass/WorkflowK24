using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Common.Interfaces;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// Infrastructure email service adapter for dispatching workflow notifications.
/// </summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Workflow automated email notification sent. To: {To}, Subject: {Subject}, IsHtml: {IsHtml}",
            to, subject, isHtml);

        return Task.CompletedTask;
    }
}
