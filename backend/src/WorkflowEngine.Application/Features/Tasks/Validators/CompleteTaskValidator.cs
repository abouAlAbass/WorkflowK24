using FluentValidation;
using WorkflowEngine.Application.Features.Tasks.Commands;

namespace WorkflowEngine.Application.Features.Tasks.Validators;

public class CompleteTaskValidator : AbstractValidator<CompleteTaskCommand>
{
    public CompleteTaskValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("TaskId is required.");

        RuleFor(x => x.Outcome)
            .NotEmpty().WithMessage("Outcome decision is required.");

        RuleFor(x => x.CompletedBy)
            .NotEmpty().WithMessage("CompletedBy user email/ID is required.");
    }
}
