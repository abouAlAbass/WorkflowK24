namespace WorkflowEngine.Application.Common.Exceptions;

public sealed class SmartFormValidationFailedException : Exception
{
    public const string ValidationFailedCode = "FORM_VALIDATION_FAILED";
    public const string ReadOnlyModifiedCode = "READ_ONLY_FIELD_MODIFIED";
    public const string PayloadTooLargeCode = "PAYLOAD_TOO_LARGE";

    public string ErrorCode { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public SmartFormValidationFailedException(
        string errorCode,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Errors = errors ?? new Dictionary<string, string[]>
        {
            ["_form"] = [message]
        };
    }
}

/// <summary>
/// Raised when a runtime submission is attempted against a SmartForm that has no Published version.
/// Mapped to HTTP 422 Unprocessable Entity with code FORM_NOT_PUBLISHED.
/// </summary>
public sealed class SmartFormNotPublishedException : InvalidOperationException
{
    public const string ErrorCode = "FORM_NOT_PUBLISHED";

    public SmartFormNotPublishedException(string message) : base(message)
    {
    }
}

/// <summary>
/// Raised when SmartForm rules contain a circular dependency in the evaluation DAG.
/// Mapped to HTTP 422 Unprocessable Entity with code CYCLIC_FORM_RULE.
/// </summary>
public sealed class CyclicFormRuleException : InvalidOperationException
{
    public const string ErrorCode = "CYCLIC_FORM_RULE";

    public IReadOnlyList<string> CyclePath { get; }

    public CyclicFormRuleException(string message, IReadOnlyList<string> cyclePath)
        : base(message)
    {
        CyclePath = cyclePath;
    }
}
