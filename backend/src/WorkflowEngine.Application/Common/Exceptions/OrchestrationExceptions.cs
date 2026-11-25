namespace WorkflowEngine.Application.Common.Exceptions;

/// <summary>
/// Raised when the linked workflow cannot be started during a form submission
/// (unknown/unpublished workflow, invalid graph, or failed trigger execution).
/// The surrounding transaction is rolled back; the API maps it to HTTP 422.
/// </summary>
public sealed class WorkflowStartFailedException : Exception
{
    public const string ErrorCode = "WORKFLOW_START_FAILED";

    public WorkflowStartFailedException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// Raised on an optimistic-concurrency violation (stale ExpectedVersion supplied by the client).
/// Mapped to HTTP 409 with code CONCURRENT_MODIFICATION.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public const string ErrorCode = "CONCURRENT_MODIFICATION";

    public ConcurrencyConflictException(string message) : base(message)
    {
    }
}

/// <summary>
/// Raised when an Idempotency-Key is replayed by a different caller than the original submitter.
/// </summary>
public sealed class IdempotencyKeyConflictException : Exception
{
    public const string ErrorCode = "IDEMPOTENCY_KEY_CONFLICT";

    public IdempotencyKeyConflictException(string message) : base(message)
    {
    }
}

/// <summary>
/// Raised when a task completion is attempted on a suspended workflow instance.
/// Mapped to HTTP 409 Conflict with code INSTANCE_SUSPENDED.
/// </summary>
public sealed class InstanceSuspendedException : Exception
{
    public const string ErrorCode = "INSTANCE_SUSPENDED";

    public InstanceSuspendedException(string message) : base(message)
    {
    }
}
