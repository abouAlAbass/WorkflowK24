namespace WorkflowEngine.Application.Common.Exceptions;

/// <summary>
/// Raised when an authenticated caller lacks permissions for a workflow instance or task operation.
/// Mapped to HTTP 403 Forbidden.
/// </summary>
public sealed class WorkflowForbiddenException : Exception
{
    public const string ErrorCode = "FORBIDDEN";

    public WorkflowForbiddenException(string message) : base(message)
    {
    }
}
