namespace WorkflowEngine.Domain.Exceptions;

/// <summary>
/// Raised when an attempt is made to mutate a Published or Archived SmartFormVersion.
/// Mapped to HTTP 409 Conflict with code FORM_VERSION_IMMUTABLE.
/// </summary>
public sealed class SmartFormVersionImmutableException : InvalidOperationException
{
    public const string ErrorCode = "FORM_VERSION_IMMUTABLE";

    public SmartFormVersionImmutableException(string message) : base(message)
    {
    }
}
