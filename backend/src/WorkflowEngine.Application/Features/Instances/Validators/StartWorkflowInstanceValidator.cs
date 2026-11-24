using FluentValidation;
using WorkflowEngine.Application.Features.Instances.Commands;

namespace WorkflowEngine.Application.Features.Instances.Validators;

public class StartWorkflowInstanceValidator : AbstractValidator<StartWorkflowInstanceCommand>
{
    public StartWorkflowInstanceValidator()
    {
        RuleFor(x => x.WorkflowKey)
            .NotEmpty().WithMessage("WorkflowKey is required.");

        RuleFor(x => x.InitiatorId)
            .NotEmpty().WithMessage("InitiatorId is required.");
    }
}
