using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Features.SmartForms.Validation;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowExecutionEngine, WorkflowExecutionEngine>();
        services.AddScoped<IWorkflowInstanceStarter, WorkflowInstanceStarter>();
        services.AddScoped<ISmartFormValidationEngine, SmartFormValidationEngine>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
