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
