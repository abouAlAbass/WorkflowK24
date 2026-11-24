namespace WorkflowEngine.Application.Common.Interfaces;

/// <summary>
/// Port for automated email notifications dispatched by workflow execution.
/// </summary>
public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default);
}
