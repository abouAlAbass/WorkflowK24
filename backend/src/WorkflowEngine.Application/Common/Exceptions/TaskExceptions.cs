namespace WorkflowEngine.Application.Common.Exceptions;

public sealed class TaskForbiddenException : Exception
{
    public TaskForbiddenException(string message) : base(message)
    {
    }
}

public sealed class FormReadOnlyException : Exception
{
    public const string ErrorCode = "FORM_READ_ONLY";

    public FormReadOnlyException(string message) : base(message)
    {
    }
}

public sealed class DelegateNotAuthorizedException : Exception
{
    public const string ErrorCode = "DELEGATE_NOT_AUTHORIZED";

    public DelegateNotAuthorizedException(string message) : base(message)
    {
    }
}

public sealed class DelegationCycleException : Exception
{
    public const string ErrorCode = "DELEGATION_CYCLE_DETECTED";

    public DelegationCycleException(string message) : base(message)
    {
    }
}

public sealed class DelegationChainTooDeepException : Exception
{
    public const string ErrorCode = "DELEGATION_CHAIN_TOO_DEEP";

    public DelegationChainTooDeepException(string message) : base(message)
    {
    }
}

public sealed class DelegationValidationException : Exception
{
    public const string ErrorCode = "DELEGATION_VALIDATION_ERROR";

    public DelegationValidationException(string message) : base(message)
    {
    }
}

public sealed class TaskAlreadyClaimedException : Exception
{
    public const string ErrorCode = "TASK_ALREADY_CLAIMED";

    public TaskAlreadyClaimedException(string message) : base(message)
    {
    }
}

public sealed class TaskClaimedByAnotherUserException : Exception
{
    public const string ErrorCode = "TASK_CLAIMED_BY_ANOTHER_USER";

    public TaskClaimedByAnotherUserException(string message) : base(message)
    {
    }
}

public sealed class SlotAlreadyCompletedException : Exception
{
    public const string ErrorCode = "SLOT_ALREADY_COMPLETED";

    public SlotAlreadyCompletedException(string message) : base(message)
    {
    }
}
