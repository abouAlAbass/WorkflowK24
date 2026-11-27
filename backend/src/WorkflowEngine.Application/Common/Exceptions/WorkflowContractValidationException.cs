namespace WorkflowEngine.Application.Common.Exceptions;

/// <summary>
/// Thrown when publishing a workflow definition fails static contract validation
/// (e.g., invalid SmartObject@Version, unpublished object, unknown method, or incompatible input/output types).
/// Mapped to HTTP 422 Unprocessable Entity.
/// </summary>
public class WorkflowContractValidationException : Exception
{
    public const string DefaultErrorCode = "WORKFLOW_CONTRACT_VALIDATION_FAILED";

    public string ErrorCode { get; }
    public IReadOnlyList<string> Errors { get; }

    public WorkflowContractValidationException(
        string message,
        IReadOnlyList<string> errors,
        string errorCode = DefaultErrorCode)
        : base(message)
    {
        ErrorCode = errorCode;
        Errors = errors ?? Array.Empty<string>();
    }
}
