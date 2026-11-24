using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Repositories;
using WorkflowEngine.Infrastructure.Services;

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
        services.AddScoped<IWorkflowAuditRepository, WorkflowAuditRepository>();
        services.AddScoped<IWorkflowErrorRepository, WorkflowErrorRepository>();
        services.AddScoped<ISmartObjectRepository, SmartObjectRepository>();
        services.AddScoped<ISystemUserRepository, SystemUserRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // External services
        services.AddScoped<IEmailService, LoggingEmailService>();

        return services;
    }
}
