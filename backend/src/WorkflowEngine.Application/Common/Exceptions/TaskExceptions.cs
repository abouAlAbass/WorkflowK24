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
