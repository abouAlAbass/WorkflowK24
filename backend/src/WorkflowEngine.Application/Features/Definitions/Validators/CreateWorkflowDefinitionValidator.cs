using System.Text.Json;
using FluentValidation;
using WorkflowEngine.Application.Features.Definitions.Commands;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Features.Definitions.Validators;

public class CreateWorkflowDefinitionValidator : AbstractValidator<CreateWorkflowDefinitionCommand>
{
    private const int MaxGraphBytes = 1_000_000; // 1 MB DoS guard
    private const int MaxNodesCount = 500;

    public CreateWorkflowDefinitionValidator()
    {
        RuleFor(x => x.WorkflowKey)
            .NotEmpty().WithMessage("WorkflowKey is required.")
            .MaximumLength(100).WithMessage("WorkflowKey cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("WorkflowKey must be alphanumeric with dashes or underscores.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workflow name is required.")
            .MaximumLength(255).WithMessage("Workflow name cannot exceed 255 characters.")
            .Must(NotContainScriptTag).WithMessage("Workflow name contains disallowed script markup.");

        RuleFor(x => x.GraphJson)
            .NotEmpty().WithMessage("GraphJson payload is required.")
            .MaximumLength(MaxGraphBytes).WithMessage("GraphJson payload exceeds maximum allowed size of 1 MB.")
            .Must(BeValidWorkflowGraph).WithMessage("GraphJson must be a valid workflow graph with unique node IDs and <= 500 nodes.");

        RuleFor(x => x.CreatedBy)
            .NotEmpty().WithMessage("CreatedBy identifier is required.")
            .MaximumLength(255);
    }

    private static bool NotContainScriptTag(string? input)
    {
        return string.IsNullOrEmpty(input) || !input.Contains("<script", StringComparison.OrdinalIgnoreCase);
    }

    private static bool BeValidWorkflowGraph(string? graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(graphJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("nodes", out var nodesProp) || nodesProp.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            int count = nodesProp.GetArrayLength();
            if (count > MaxNodesCount)
            {
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodesProp.EnumerateArray())
            {
                if (node.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                {
                    var id = idProp.GetString();
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
