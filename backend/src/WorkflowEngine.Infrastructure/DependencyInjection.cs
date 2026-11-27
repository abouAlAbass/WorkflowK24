using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Repositories;
using WorkflowEngine.Infrastructure.Services;
using WorkflowEngine.Infrastructure.Services.Brokers;

namespace WorkflowEngine.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services including EF Core DbContext with multi-provider support,
    /// repository adapters, and external communication services.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"];
        var connectionString = configuration["Database:ConnectionString"]
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException(
                "Database provider is not configured. Set 'Database:Provider' to 'PostgreSql' or 'SqlServer' in appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is not configured. Set 'Database:ConnectionString' in appsettings.json.");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        if (normalizedProvider != "postgresql" && normalizedProvider != "sqlserver")
        {
            throw new InvalidOperationException(
                $"Unsupported database provider '{provider}'. Supported providers are 'PostgreSql' and 'SqlServer'.");
        }

        services.AddDbContext<WorkflowEngineDbContext>(options =>
        {
            switch (provider.Trim().ToLowerInvariant())
            {
                case "postgresql":
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(typeof(WorkflowEngineDbContext).Assembly.FullName);
                        npgsqlOptions.EnableRetryOnFailure(3);
                    });
                    break;

                case "sqlserver":
                    options.UseSqlServer(connectionString, sqlServerOptions =>
                    {
                        sqlServerOptions.MigrationsAssembly(typeof(WorkflowEngineDbContext).Assembly.FullName);
                        sqlServerOptions.EnableRetryOnFailure(3);
                    });
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider '{provider}'. Supported providers are 'PostgreSql' and 'SqlServer'.");
            }
        });

        // Repositories & Unit of Work
        services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<IWorkflowTaskRepository, WorkflowTaskRepository>();
        services.AddScoped<IDelegationRuleRepository, DelegationRuleRepository>();
        services.AddScoped<IWorkflowAuditRepository, WorkflowAuditRepository>();
        services.AddScoped<IWorkflowErrorRepository, WorkflowErrorRepository>();
        services.AddScoped<IInstanceEventRepository, InstanceEventRepository>();
        services.AddScoped<INodeExecutionRepository, NodeExecutionRepository>();
        services.AddScoped<ICommandLogRepository, CommandLogRepository>();
        services.AddScoped<ISmartObjectRepository, SmartObjectRepository>();
        services.AddScoped<ISmartFormRepository, SmartFormRepository>();
        services.AddScoped<ISystemUserRepository, SystemUserRepository>();
        services.AddScoped<IUserAuditRepository, UserAuditRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<IConnectionRepository, ConnectionRepository>();
        services.AddScoped<IConnectionPermissionRepository, ConnectionPermissionRepository>();
        services.AddScoped<IUserConnectionCredentialRepository, UserConnectionCredentialRepository>();
        services.AddScoped<IConnectionEnvironmentConfigRepository, ConnectionEnvironmentConfigRepository>();
        services.AddScoped<IConnectionUsageLogRepository, ConnectionUsageLogRepository>();
        services.AddScoped<IAuthProviderRepository, AuthProviderRepository>();
        services.AddScoped<IExternalUserLoginRepository, ExternalUserLoginRepository>();
        services.AddScoped<IExternalUserTokenRepository, ExternalUserTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Data Protection key ring persisted in database for multi-instance & restart durability
        services.AddDataProtection()
            .SetApplicationName("TamamWorkflowStudio")
            .PersistKeysToDbContext<WorkflowEngineDbContext>();

        // External & security services
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddSingleton<IBlobStorageService, LocalBlobStorageService>();
        services.AddSingleton<IExternalVaultClient, ConfigurationExternalVaultClient>();
        services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<IAuditService, SecurityAuditService>();
        services.AddSingleton<IHttpConnectorTransport, HttpConnectorTransport>();
        services.AddSingleton<IPostgresConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<ISqlServerCommandRunner, SqlClientCommandRunner>();

        // SharePoint Microsoft Graph named HttpClient with strict anti-SSRF (AllowAutoRedirect = false)
        // and Polly resilience (5xx + 429 Retry-After + circuit breaker).
        services.AddHttpClient(SharePointServiceBroker.GraphHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 50
            })
            .AddResilienceHandler("sharepoint-graph-pipeline", resilienceBuilder =>
            {
                resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromMilliseconds(200),
                    UseJitter = true,
                    ShouldHandle = args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException ||
                        (args.Outcome.Result is HttpResponseMessage resp &&
                         (resp.StatusCode == HttpStatusCode.TooManyRequests || (int)resp.StatusCode >= 500))),
                    DelayGenerator = args =>
                    {
                        var retryAfter = args.Outcome.Result?.Headers.RetryAfter;
                        if (retryAfter != null)
                        {
                            if (retryAfter.Delta.HasValue)
                            {
                                var delta = retryAfter.Delta.Value;
                                return ValueTask.FromResult<TimeSpan?>(delta < TimeSpan.Zero ? TimeSpan.Zero : delta);
                            }

                            if (retryAfter.Date.HasValue)
                            {
                                var wait = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                                return ValueTask.FromResult<TimeSpan?>(wait < TimeSpan.Zero ? TimeSpan.Zero : wait);
                            }
                        }

                        return ValueTask.FromResult<TimeSpan?>(null);
                    }
                });

                resilienceBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15),
                    ShouldHandle = args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException ||
                        (args.Outcome.Result is HttpResponseMessage resp &&
                         (resp.StatusCode == HttpStatusCode.TooManyRequests || (int)resp.StatusCode >= 500)))
                });
            });

        // Entra ID OAuth2 token client (AllowAutoRedirect = false)
        services.AddHttpClient(SharePointServiceBroker.TokenHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            });

        // OAuth2 On-Behalf-Of (OBO) token exchange client (AllowAutoRedirect = false)
        services.AddHttpClient(OboTokenExchangeService.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            });

        services.AddScoped<IOboTokenExchangeService, OboTokenExchangeService>();
        services.AddSingleton<IConnectionUsageRecorder, ConnectionUsageRecorder>();

        services.AddScoped<IServiceBroker, PostgresServiceBroker>();
        services.AddScoped<IServiceBroker, SqlServerServiceBroker>();
        services.AddScoped<IServiceBroker, SharePointServiceBroker>();

        return services;
    }
}
