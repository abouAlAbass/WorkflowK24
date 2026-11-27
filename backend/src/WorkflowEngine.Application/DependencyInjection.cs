using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Features.SmartForms.Validation;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Application.Services.Connections;
using WorkflowEngine.Application.Services.Executors;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Application.Services.SmartObjects;

namespace WorkflowEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IDnsResolver, SystemDnsResolver>();
        services.AddSingleton<ISsrfGuard, SsrfGuard>();

        services.AddScoped<IExecutionSecretResolver, ExecutionSecretResolver>();
        services.AddScoped<IConnectionAccessService, ConnectionAccessService>();
        services.AddScoped<IServiceBroker, RestServiceBroker>();
        services.AddScoped<IServiceBrokerRegistry, ServiceBrokerRegistry>();

        services.AddScoped<ISmartObjectProvider, InternalStoreSmartObjectProvider>();
        services.AddScoped<ISmartObjectProvider, HttpBindingSmartObjectProvider>();
        services.AddScoped<ISmartObjectProvider, ServiceBrokerSmartObjectProvider>();
        services.AddScoped<ISmartObjectContractValidator, SmartObjectContractValidator>();
        services.AddSingleton<IParallelGraphValidator, ParallelGraphValidator>();

        services.AddScoped<INodeExecutor, HttpNodeExecutor>();
        services.AddScoped<INodeExecutor, SmartObjectNodeExecutor>();
        services.AddScoped<INodeExecutorRegistry, NodeExecutorRegistry>();

        services.AddScoped<IWorkflowExecutionEngine, WorkflowExecutionEngine>();
        services.AddScoped<IWorkflowInstanceStarter, WorkflowInstanceStarter>();
        services.AddScoped<ITaskEscalationService, TaskEscalationService>();
        services.AddScoped<ISmartFormValidationEngine, SmartFormValidationEngine>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
