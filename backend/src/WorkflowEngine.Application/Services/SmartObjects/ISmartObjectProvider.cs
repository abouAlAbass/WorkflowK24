using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Services.SmartObjects;

public enum SmartObjectErrorCategory
{
    None = 0,
    Business = 1,
    Technical = 2,
    Fatal = 3
}

public static class SmartObjectErrorCodes
{
    // BUSINESS errors (routable via onBusinessError, never auto-retried)
    public const string NotFound = "NOT_FOUND";
    public const string Validation = "VALIDATION";
    public const string Conflict = "CONFLICT";
    public const string Permission = "PERMISSION";

    // TECHNICAL errors (retriable ONLY when method.idempotent == true)
    public const string Unavailable = "UNAVAILABLE";
    public const string Timeout = "TIMEOUT";

    // FATAL errors (never retried, immediate Faulted)
    public const string ConfigInvalid = "SMARTOBJECT_CONFIG_INVALID";
    public const string ObjectNotFound = "SMARTOBJECT_NOT_FOUND";
    public const string ObjectNotPublished = "SMARTOBJECT_NOT_PUBLISHED";
    public const string MethodNotFound = "SMARTOBJECT_METHOD_NOT_FOUND";
    public const string ProviderNotFound = "SMARTOBJECT_PROVIDER_NOT_FOUND";
    public const string ConnectionNotFound = "SMARTOBJECT_CONNECTION_NOT_FOUND";
    public const string SecretResolutionFailed = "SMARTOBJECT_SECRET_RESOLUTION_FAILED";
    public const string SsrfBlocked = "SSRF_BLOCKED";
    public const string ResponseTooLarge = "SMARTOBJECT_RESPONSE_TOO_LARGE";
}

public sealed record SmartObjectProviderContext(
    SmartObjectEntity SmartObject,
    SmartObjectMethodDto Method,
    Dictionary<string, object?> Inputs,
    WorkflowInstance Instance,
    string NodeId,
    string IdempotencyKey,
    List<string> SecretsToRedact
);

public sealed record SmartObjectProviderResult(
    bool IsSuccess,
    Dictionary<string, object?> Outputs,
    SmartObjectErrorCategory ErrorCategory = SmartObjectErrorCategory.None,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int? StatusCode = null
)
{
    public static SmartObjectProviderResult Success(Dictionary<string, object?> outputs)
        => new(true, outputs ?? new Dictionary<string, object?>(), SmartObjectErrorCategory.None);

    public static SmartObjectProviderResult BusinessError(string code, string message, int? statusCode = null)
        => new(false, new Dictionary<string, object?>(), SmartObjectErrorCategory.Business, code, message, statusCode);

    public static SmartObjectProviderResult TechnicalError(string code, string message, int? statusCode = null)
        => new(false, new Dictionary<string, object?>(), SmartObjectErrorCategory.Technical, code, message, statusCode);

    public static SmartObjectProviderResult FatalError(string code, string message)
        => new(false, new Dictionary<string, object?>(), SmartObjectErrorCategory.Fatal, code, message);
}

/// <summary>
/// Pluggable execution provider for SmartObject methods (e.g. InternalStore, HttpBinding).
/// </summary>
public interface ISmartObjectProvider
{
    string ProviderName { get; }
    bool CanHandle(string providerName);
    Task<SmartObjectProviderResult> ExecuteAsync(SmartObjectProviderContext context, CancellationToken ct = default);
}
