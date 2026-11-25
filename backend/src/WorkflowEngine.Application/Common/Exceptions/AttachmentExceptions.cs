namespace WorkflowEngine.Application.Common.Exceptions;

public sealed class AttachmentForbiddenException : Exception
{
    public AttachmentForbiddenException(string message) : base(message)
    {
    }
}

public sealed class AttachmentValidationException : Exception
{
    public const string InvalidFileTypeCode = "INVALID_FILE_TYPE";
    public const string MalwareDetectedCode = "MALWARE_DETECTED";
    public const string FileTooLargeCode = "FILE_TOO_LARGE";

    public string ErrorCode { get; }

    public AttachmentValidationException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
