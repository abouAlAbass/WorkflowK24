using FluentValidation;
using WorkflowEngine.Application.Features.Instances.Commands;

namespace WorkflowEngine.Application.Features.Instances.Validators;

public class SuspendWorkflowInstanceValidator : AbstractValidator<SuspendWorkflowInstanceCommand>
{
    public SuspendWorkflowInstanceValidator()
    {
        RuleFor(x => x.CommandId).NotEmpty().WithMessage("CommandId is required.");
    }
}

public class ResumeWorkflowInstanceValidator : AbstractValidator<ResumeWorkflowInstanceCommand>
{
    public ResumeWorkflowInstanceValidator()
    {
        RuleFor(x => x.CommandId).NotEmpty().WithMessage("CommandId is required.");
    }
}

public class CancelWorkflowInstanceValidator : AbstractValidator<CancelWorkflowInstanceCommand>
{
    public CancelWorkflowInstanceValidator()
    {
        RuleFor(x => x.CommandId).NotEmpty().WithMessage("CommandId is required.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length >= 10)
            .WithMessage("Cancellation reason must be at least 10 characters long.");
    }
}

public class RetryWorkflowInstanceValidator : AbstractValidator<RetryWorkflowInstanceCommand>
{
    public RetryWorkflowInstanceValidator()
    {
        RuleFor(x => x.CommandId).NotEmpty().WithMessage("CommandId is required.");
        RuleFor(x => x.Mode)
            .Must(m => string.IsNullOrWhiteSpace(m) || m == "retry_failed_node" || m == "retry_from_node")
            .WithMessage("Invalid retry mode. Must be 'retry_failed_node' or 'retry_from_node'.");
    }
}
