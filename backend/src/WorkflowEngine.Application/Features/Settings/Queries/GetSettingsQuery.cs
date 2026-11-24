using WorkflowEngine.Application.Features.Settings.DTOs;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Settings.Queries;

public record GetSettingsQuery;

public static class GetSettingsHandler
{
    public static async Task<SystemSettingsDto> Handle(
        GetSettingsQuery query,
        ISystemSettingRepository repository,
        CancellationToken ct)
    {
        var settings = await repository.GetAllAsync(ct);
        var map = settings.ToDictionary(s => s.Key, s => s.Value);

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
