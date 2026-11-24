using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowExecutionEngine, WorkflowExecutionEngine>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
