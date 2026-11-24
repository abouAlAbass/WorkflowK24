using FluentValidation;
using WorkflowEngine.Application.Features.Definitions.Commands;

namespace WorkflowEngine.Application.Features.Definitions.Validators;

public class CreateWorkflowDefinitionValidator : AbstractValidator<CreateWorkflowDefinitionCommand>
{
    public CreateWorkflowDefinitionValidator()
    {
        RuleFor(x => x.WorkflowKey)
            .NotEmpty().WithMessage("WorkflowKey is required.")
            .MaximumLength(100).WithMessage("WorkflowKey cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("WorkflowKey must be alphanumeric with dashes or underscores.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workflow name is required.")
            .MaximumLength(255).WithMessage("Workflow name cannot exceed 255 characters.");

        RuleFor(x => x.GraphJson)
            .NotEmpty().WithMessage("GraphJson payload is required.");

        RuleFor(x => x.CreatedBy)
            .NotEmpty().WithMessage("CreatedBy identifier is required.")
            .MaximumLength(255);
    }
}
