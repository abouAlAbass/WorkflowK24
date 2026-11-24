using FluentValidation;
using WorkflowEngine.Application.Features.Settings.DTOs;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Settings.Commands;

public record UpdateSettingsCommand(
    string? SmtpHost = null,
    string? SmtpPort = null,
    string? SmtpSecurity = null,
    string? SqlConnectionString = null,
    int? SqlMaxPoolSize = null,
    int? SqlTimeoutSeconds = null,
    int? RetentionDays = null,
    bool? TelemetryEnabled = null,
    bool? MfaEnforced = null
);

public class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsCommandValidator()
    {
        When(x => x.SmtpPort != null, () =>
        {
            RuleFor(x => x.SmtpPort).Matches(@"^\d+$").WithMessage("SMTP Port must be a numeric string.");
        });
        When(x => x.SqlMaxPoolSize != null, () =>
        {
            RuleFor(x => x.SqlMaxPoolSize).GreaterThan(0).LessThanOrEqualTo(1000);
        });
    }
}

public static class UpdateSettingsHandler
{
    public static async Task<SystemSettingsDto> Handle(
        UpdateSettingsCommand command,
        ISystemSettingRepository repository,
        CancellationToken ct)
    {
        if (command.SmtpHost != null)
            await repository.SetAsync("smtp.server", command.SmtpHost, "Email", ct);

        if (command.SmtpPort != null)
            await repository.SetAsync("smtp.port", command.SmtpPort, "Email", ct);

        if (command.SmtpSecurity != null)
            await repository.SetAsync("smtp.security", command.SmtpSecurity, "Email", ct);

        if (command.SqlConnectionString != null)
            await repository.SetAsync("sql.connection_string", command.SqlConnectionString, "Database", ct);

        if (command.SqlMaxPoolSize.HasValue)
            await repository.SetAsync("sql.max_pool_size", command.SqlMaxPoolSize.Value.ToString(), "Database", ct);

        if (command.SqlTimeoutSeconds.HasValue)
            await repository.SetAsync("sql.timeout_sec", command.SqlTimeoutSeconds.Value.ToString(), "Database", ct);

        if (command.RetentionDays.HasValue)
            await repository.SetAsync("system.retention_days", command.RetentionDays.Value.ToString(), "System", ct);

        if (command.TelemetryEnabled.HasValue)
            await repository.SetAsync("telemetry.enabled", command.TelemetryEnabled.Value.ToString().ToLowerInvariant(), "Monitoring", ct);

        if (command.MfaEnforced.HasValue)
            await repository.SetAsync("security.mfa_enforced", command.MfaEnforced.Value.ToString().ToLowerInvariant(), "Security", ct);

        await repository.SaveChangesAsync(ct);

        var all = await repository.GetAllAsync(ct);
        var map = all.ToDictionary(s => s.Key, s => s.Value);

        return new SystemSettingsDto(
            SmtpHost: map.GetValueOrDefault("smtp.server", "smtp.mail.enterprise.com"),
            SmtpPort: map.GetValueOrDefault("smtp.port", "587"),
            SmtpSecurity: map.GetValueOrDefault("smtp.security", "STARTTLS"),
            SqlConnectionString: map.GetValueOrDefault("sql.connection_string", "Server=sql-prod.internal;Database=K2_WorkflowDB;User Id=k2_admin;Password=********;"),
            SqlMaxPoolSize: int.TryParse(map.GetValueOrDefault("sql.max_pool_size", "100"), out var pool) ? pool : 100,
            SqlTimeoutSeconds: int.TryParse(map.GetValueOrDefault("sql.timeout_sec", "30"), out var timeout) ? timeout : 30,
            RetentionDays: int.TryParse(map.GetValueOrDefault("system.retention_days", "90"), out var ret) ? ret : 90,
            TelemetryEnabled: bool.TryParse(map.GetValueOrDefault("telemetry.enabled", "true"), out var tel) && tel,
            MfaEnforced: bool.TryParse(map.GetValueOrDefault("security.mfa_enforced", "false"), out var mfa) && mfa
        );
    }
}
