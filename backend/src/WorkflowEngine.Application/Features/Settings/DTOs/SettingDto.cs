namespace WorkflowEngine.Application.Features.Settings.DTOs;

public record SettingItemDto(
    string Key,
    string Value,
    string Category
);

public record SystemSettingsDto(
    string SmtpHost,
    string SmtpPort,
    string SmtpSecurity,
    string SqlConnectionString,
    int SqlMaxPoolSize,
    int SqlTimeoutSeconds,
    int RetentionDays,
    bool TelemetryEnabled,
    bool MfaEnforced
);
