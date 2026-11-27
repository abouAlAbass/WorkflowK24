using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Application.Services.SmartObjects;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Services.Executors;

/// <summary>
/// Pluggable node executor for versioned SmartObject actions (ObjectKey@Version).
/// - Resolved via INodeExecutorRegistry (no hardcoded switch in WorkflowExecutionEngine)
/// - Delegates execution to ISmartObjectProvider (InternalStore, HttpBinding)
/// - Normalizes errors into BUSINESS (routable via onBusinessError), TECHNICAL (retried ONLY if idempotent == true), or FATAL
/// - Emits SmartObject.Invoked audit event with sensitive values masked
/// </summary>
public class SmartObjectNodeExecutor : INodeExecutor
{
    private static readonly HashSet<string> SupportedNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "smartobject",
        "smartObject",
        "smart_object"
    };

    private static readonly string[] SensitiveKeySubstrings =
    {
        "password",
        "secret",
        "token",
        "apikey",
        "api_key",
        "authorization",
        "credential",
        "ssn",
        "creditcard",
        "cardnumber"
    };

    private readonly ISmartObjectRepository _smartObjectRepository;
    private readonly IReadOnlyList<ISmartObjectProvider> _providers;

    public SmartObjectNodeExecutor(
        ISmartObjectRepository smartObjectRepository,
        IEnumerable<ISmartObjectProvider> providers)
    {
        _smartObjectRepository = smartObjectRepository ?? throw new ArgumentNullException(nameof(smartObjectRepository));
        _providers = providers?.ToList() ?? new List<ISmartObjectProvider>();
    }

    public bool CanExecute(string nodeType) => SupportedNodeTypes.Contains(nodeType);

    public static bool TryParseSmartObjectRef(string? smartObjectRef, out string objectKey, out int version)
    {
        objectKey = string.Empty;
        version = 0;

        if (string.IsNullOrWhiteSpace(smartObjectRef))
        {
            return false;
        }

        int atIndex = smartObjectRef.LastIndexOf('@');
        if (atIndex <= 0 || atIndex >= smartObjectRef.Length - 1)
        {
            return false;
        }

        objectKey = smartObjectRef[..atIndex].Trim();
        string versionPart = smartObjectRef[(atIndex + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(objectKey) && int.TryParse(versionPart, out version) && version >= 1;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var instance = context.Instance;
        var node = context.Node;
        var soData = node.Data.SmartObjectData;
        var sw = Stopwatch.StartNew();

        if (soData == null || string.IsNullOrWhiteSpace(soData.SmartObjectRef) || string.IsNullOrWhiteSpace(soData.MethodName))
        {
            sw.Stop();
            EmitInvokedEvent(instance, node.Id, soData?.SmartObjectRef ?? "", 0, soData?.MethodName ?? "", "none",
                "FatalError", "FATAL", SmartObjectErrorCodes.ConfigInvalid, sw.ElapsedMilliseconds, 1, null, null, Array.Empty<string>());
            return NodeExecutionResult.Faulted(
                SmartObjectErrorCodes.ConfigInvalid,
                "SmartObject node is missing required 'smartObjectRef' or 'methodName'.");
        }

        if (!TryParseSmartObjectRef(soData.SmartObjectRef, out string objectKey, out int version))
        {
            sw.Stop();
            EmitInvokedEvent(instance, node.Id, soData.SmartObjectRef, 0, soData.MethodName, "none",
                "FatalError", "FATAL", SmartObjectErrorCodes.ConfigInvalid, sw.ElapsedMilliseconds, 1, null, null, Array.Empty<string>());
            return NodeExecutionResult.Faulted(
                SmartObjectErrorCodes.ConfigInvalid,
                $"Invalid smartObjectRef '{soData.SmartObjectRef}'. Expected 'ObjectKey@Version' (e.g. 'Customer@2').");
        }

        var smartObject = await _smartObjectRepository.GetByNameAndVersionAsync(objectKey, version, ct);
        if (smartObject == null)
        {
            sw.Stop();
            EmitInvokedEvent(instance, node.Id, objectKey, version, soData.MethodName, "none",
                "FatalError", "FATAL", SmartObjectErrorCodes.ObjectNotFound, sw.ElapsedMilliseconds, 1, null, null, Array.Empty<string>());
            return NodeExecutionResult.Faulted(
                SmartObjectErrorCodes.ObjectNotFound,
                $"SmartObject '{objectKey}@{version}' was not found.");
        }

        if (smartObject.Status != SmartObjectStatus.Published)
        {
            sw.Stop();
            EmitInvokedEvent(instance, node.Id, objectKey, version, soData.MethodName, "none",
                "FatalError", "FATAL", SmartObjectErrorCodes.ObjectNotPublished, sw.ElapsedMilliseconds, 1, null, null, Array.Empty<string>());
            return NodeExecutionResult.Faulted(
                SmartObjectErrorCodes.ObjectNotPublished,
                $"SmartObject '{smartObject.ObjectRef}' has status '{smartObject.Status}' and is not Published.");
        }

        var methods = DeserializeMethods(smartObject.MethodsJson);
        var method = methods.FirstOrDefault(m => string.Equals(m.Name, soData.MethodName, StringComparison.OrdinalIgnoreCase));
        if (method == null)
        {
            sw.Stop();
            EmitInvokedEvent(instance, node.Id, objectKey, version, soData.MethodName, "none",
                "FatalError", "FATAL", SmartObjectErrorCodes.MethodNotFound, sw.ElapsedMilliseconds, 1, null, null, Array.Empty<string>());
            return NodeExecutionResult.Faulted(
                SmartObjectErrorCodes.MethodNotFound,
                $"Method '{soData.MethodName}' is not declared on SmartObject '{smartObject.ObjectRef}'.");
        }

        string providerName = string.IsNullOrWhiteSpace(method.Binding?.Provider) ? "InternalStore" : method.Binding.Provider.Trim();
        var provider = _providers.FirstOrDefault(p => p.CanHandle(providerName));
        if (provider == null)
        {
            sw.Stop();
            EmitInvokedEvent(instance, node.Id, objectKey, version, method.Name, providerName,
                "FatalError", "FATAL", SmartObjectErrorCodes.ProviderNotFound, sw.ElapsedMilliseconds, 1, null, null, Array.Empty<string>());
            return NodeExecutionResult.Faulted(
                SmartObjectErrorCodes.ProviderNotFound,
                $"No ISmartObjectProvider registered for provider '{providerName}'.");
        }

        string executionKey = $"{instance.InstanceId}:{node.Id}";
        string idempotencyKey = !string.IsNullOrWhiteSpace(soData.IdempotencyKey)
            ? SafeTemplateResolver.ResolveString(soData.IdempotencyKey, instance, executionKey)
            : executionKey;

        // Resolve inputs via SafeTemplateResolver
        var resolvedInputs = ResolveInputs(soData.InputMapping, instance, executionKey);
        var secretsToRedact = new List<string>();
        var sensitiveInputNames = new HashSet<string>(
            (method.Inputs ?? new List<SmartObjectMethodParameterDto>()).Where(i => i.IsSensitive).Select(i => i.Name),
            StringComparer.OrdinalIgnoreCase);
        var sensitiveOutputNames = new HashSet<string>(
            (method.Outputs ?? new List<SmartObjectMethodParameterDto>()).Where(o => o.IsSensitive).Select(o => o.Name),
            StringComparer.OrdinalIgnoreCase);
        var maskedFieldNames = new HashSet<string>(
            DeserializeFields(smartObject.FieldsJson).Where(f => f.Masked && !string.IsNullOrWhiteSpace(f.Name)).Select(f => f.Name.Trim()),
            StringComparer.OrdinalIgnoreCase);

        // Runtime required input check -> BUSINESS / VALIDATION
        foreach (var reqInput in (method.Inputs ?? new List<SmartObjectMethodParameterDto>()).Where(i => i.Required))
        {
            if (!resolvedInputs.TryGetValue(reqInput.Name, out var val) || IsNullOrEmptyValue(val))
            {
                sw.Stop();
                string msg = $"Required input '{reqInput.Name}' is missing for '{smartObject.ObjectRef}.{method.Name}'.";
                EmitInvokedEvent(instance, node.Id, objectKey, version, method.Name, provider.ProviderName,
                    "BusinessError", "BUSINESS", SmartObjectErrorCodes.Validation, sw.ElapsedMilliseconds, 1,
                    MaskDictionary(resolvedInputs, sensitiveInputNames), null, secretsToRedact);
                return HandleBusinessError(context, node, soData, SmartObjectErrorCodes.Validation, msg, secretsToRedact);
            }
        }

        // Strict idempotency retry policy:
        // TECHNICAL errors are retried ONLY when method.Idempotent == true.
        var retryPolicy = soData.RetryPolicy ?? new HttpRetryPolicyPayload();
        int maxAttempts = method.Idempotent ? Math.Clamp(retryPolicy.MaxAttempts, 1, 10) : 1;
        int initialDelayMs = Math.Clamp(retryPolicy.InitialDelayMs, 0, 30_000);
        double backoffMultiplier = retryPolicy.BackoffMultiplier >= 1.0 ? retryPolicy.BackoffMultiplier : 2.0;
        int maxDelayMs = Math.Clamp(retryPolicy.MaxDelayMs, 0, 60_000);

        SmartObjectProviderResult? lastResult = null;
        int attemptsUsed = 0;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            attemptsUsed = attempt;
            var providerContext = new SmartObjectProviderContext(
                smartObject,
                method,
                resolvedInputs,
                instance,
                node.Id,
                idempotencyKey,
                secretsToRedact);

            lastResult = await provider.ExecuteAsync(providerContext, ct);

            if (lastResult.IsSuccess)
            {
                sw.Stop();
                ApplyOutputMapping(instance, lastResult.Outputs, soData.OutputMapping, maskedFieldNames);

                EmitInvokedEvent(
                    instance,
                    node.Id,
                    objectKey,
                    version,
                    method.Name,
                    provider.ProviderName,
                    "Succeeded",
                    "NONE",
                    null,
                    sw.ElapsedMilliseconds,
                    attemptsUsed,
                    MaskDictionary(resolvedInputs, sensitiveInputNames),
                    MaskDictionary(lastResult.Outputs, sensitiveOutputNames),
                    secretsToRedact);

                return NodeExecutionResult.Completed();
            }

            if (lastResult.ErrorCategory == SmartObjectErrorCategory.Technical && method.Idempotent && attempt < maxAttempts)
            {
                double rawDelay = initialDelayMs * Math.Pow(backoffMultiplier, Math.Max(0, attempt - 1));
                int delayMs = (int)Math.Min(Math.Max(0, rawDelay), maxDelayMs);
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, ct);
                }
                continue;
            }

            break;
        }

        sw.Stop();
        lastResult ??= SmartObjectProviderResult.FatalError(SmartObjectErrorCodes.Unavailable, "SmartObject execution produced no result.");

        string categoryLabel = lastResult.ErrorCategory.ToString().ToUpperInvariant();
        string statusLabel = lastResult.ErrorCategory switch
        {
            SmartObjectErrorCategory.Business => "BusinessError",
            SmartObjectErrorCategory.Technical => "TechnicalError",
            _ => "FatalError"
        };

        EmitInvokedEvent(
            instance,
            node.Id,
            objectKey,
            version,
            method.Name,
            provider.ProviderName,
            statusLabel,
            categoryLabel,
            lastResult.ErrorCode,
            sw.ElapsedMilliseconds,
            attemptsUsed,
            MaskDictionary(resolvedInputs, sensitiveInputNames),
            null,
            secretsToRedact);

        if (lastResult.ErrorCategory == SmartObjectErrorCategory.Business)
        {
            return HandleBusinessError(
                context,
                node,
                soData,
                lastResult.ErrorCode ?? SmartObjectErrorCodes.Validation,
                lastResult.ErrorMessage ?? "SmartObject business error.",
                secretsToRedact);
        }

        return NodeExecutionResult.Faulted(
            lastResult.ErrorCode ?? SmartObjectErrorCodes.Unavailable,
            RedactSecrets(lastResult.ErrorMessage ?? "SmartObject execution failed.", secretsToRedact));
    }

    private static NodeExecutionResult HandleBusinessError(
        NodeExecutionContext context,
        ReactFlowNode node,
        SmartObjectNodeDataPayload soData,
        string errorCode,
        string errorMessage,
        IReadOnlyList<string> secretsToRedact)
    {
        string sanitizedMsg = RedactSecrets(errorMessage, secretsToRedact);
        string? routeTo = ResolveBusinessErrorTarget(context.Graph, node, soData.OnBusinessError);

        if (!string.IsNullOrWhiteSpace(routeTo))
        {
            string targetVar = !string.IsNullOrWhiteSpace(soData.OnBusinessError?.ErrorVariable)
                ? soData.OnBusinessError.ErrorVariable.Trim()
                : "lastSmartObjectError";

            JsonObject contextObj;
            try
            {
                contextObj = !string.IsNullOrWhiteSpace(context.Instance.ContextVariablesJson)
                    ? JsonNode.Parse(context.Instance.ContextVariablesJson)?.AsObject() ?? new JsonObject()
                    : new JsonObject();
            }
            catch
            {
                contextObj = new JsonObject();
            }

            contextObj[targetVar] = new JsonObject
            {
                ["category"] = "BUSINESS",
                ["code"] = errorCode,
                ["message"] = sanitizedMsg,
                ["smartObjectRef"] = soData.SmartObjectRef,
                ["method"] = soData.MethodName
            };

            context.Instance.UpdateVariables(contextObj.ToJsonString());
            return NodeExecutionResult.Completed(explicitNextNodeId: routeTo);
        }

        return NodeExecutionResult.Faulted(errorCode, sanitizedMsg);
    }

    private static string? ResolveBusinessErrorTarget(
        ReactFlowGraph graph,
        ReactFlowNode currentNode,
        HttpBusinessErrorPayload? onBusinessError)
    {
        if (!string.IsNullOrWhiteSpace(onBusinessError?.RouteTo))
        {
            string routeTo = onBusinessError.RouteTo.Trim();
            var match = graph.Nodes.FirstOrDefault(n => string.Equals(n.Id, routeTo, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.Id;
        }

        var errorEdge = graph.Edges.FirstOrDefault(e =>
            e.Source == currentNode.Id &&
            (string.Equals(e.SourceHandle, "business-error", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.SourceHandle, "on_business_error", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.Label, "on_business_error", StringComparison.OrdinalIgnoreCase)));

        return errorEdge?.Target;
    }

    private static Dictionary<string, object?> ResolveInputs(
        Dictionary<string, JsonElement>? inputMapping,
        WorkflowInstance instance,
        string executionKey)
    {
        var resolved = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (inputMapping == null || inputMapping.Count == 0) return resolved;

        foreach (var kv in inputMapping)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            var resolvedNode = SafeTemplateResolver.ResolveJsonElement(kv.Value, instance, executionKey);
            if (resolvedNode == null)
            {
                resolved[kv.Key.Trim()] = null;
            }
            else
            {
                using var doc = JsonDocument.Parse(resolvedNode.ToJsonString());
                resolved[kv.Key.Trim()] = doc.RootElement.Clone();
            }
        }

        return resolved;
    }

    private static void ApplyOutputMapping(
        WorkflowInstance instance,
        Dictionary<string, object?> outputs,
        Dictionary<string, string>? outputMapping,
        HashSet<string>? maskedFieldNames = null)
    {
        var sensitiveToAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (maskedFieldNames is { Count: > 0 })
        {
            foreach (var mf in maskedFieldNames)
            {
                sensitiveToAdd.Add(mf);
            }
        }

        if (outputMapping != null && outputMapping.Count > 0 && outputs.Count > 0)
        {
            JsonObject contextObj;
            try
            {
                contextObj = !string.IsNullOrWhiteSpace(instance.ContextVariablesJson)
                    ? JsonNode.Parse(instance.ContextVariablesJson)?.AsObject() ?? new JsonObject()
                    : new JsonObject();
            }
            catch
            {
                contextObj = new JsonObject();
            }

            using var outputsDoc = JsonDocument.Parse(JsonSerializer.Serialize(outputs));
            bool changed = false;

            foreach (var kv in outputMapping)
            {
                string targetVar = kv.Key.Trim();
                string sourceExpr = kv.Value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(targetVar) || string.IsNullOrWhiteSpace(sourceExpr)) continue;

                if (maskedFieldNames is { Count: > 0 })
                {
                    string normalizedSource = sourceExpr.StartsWith("$.", StringComparison.Ordinal)
                        ? sourceExpr[2..]
                        : sourceExpr;
                    if (maskedFieldNames.Contains(sourceExpr) || maskedFieldNames.Contains(normalizedSource))
                    {
                        sensitiveToAdd.Add(targetVar);
                    }
                }

                if (sourceExpr.StartsWith("$", StringComparison.Ordinal))
                {
                    if (SimpleJsonPathExtractor.TryExtract(outputsDoc.RootElement, sourceExpr, out var extracted))
                    {
                        contextObj[targetVar] = extracted.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                            ? null
                            : JsonNode.Parse(extracted.GetRawText());
                        changed = true;
                    }
                }
                else
                {
                    var matchedOutput = outputs.FirstOrDefault(o => string.Equals(o.Key, sourceExpr, StringComparison.OrdinalIgnoreCase));
                    if (matchedOutput.Key != null)
                    {
                        contextObj[targetVar] = matchedOutput.Value == null
                            ? null
                            : JsonNode.Parse(JsonSerializer.Serialize(matchedOutput.Value));
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                instance.UpdateVariables(contextObj.ToJsonString());
            }
        }

        if (sensitiveToAdd.Count > 0)
        {
            var mergedSensitive = new HashSet<string>(instance.GetSensitiveVariableNames(), StringComparer.OrdinalIgnoreCase);
            mergedSensitive.UnionWith(sensitiveToAdd);
            instance.SetSensitiveVariableNames(mergedSensitive);
        }
    }

    private static Dictionary<string, object?> MaskDictionary(
        Dictionary<string, object?> source,
        HashSet<string> declaredSensitiveKeys)
    {
        var masked = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in source)
        {
            bool isSensitive = declaredSensitiveKeys.Contains(kv.Key) || IsSensitiveKeyName(kv.Key);
            masked[kv.Key] = isSensitive ? "***REDACTED***" : kv.Value;
        }
        return masked;
    }

    private static bool IsSensitiveKeyName(string key)
    {
        string lower = key.ToLowerInvariant();
        return SensitiveKeySubstrings.Any(sub => lower.Contains(sub, StringComparison.Ordinal));
    }

    private static bool IsNullOrEmptyValue(object? val)
    {
        if (val == null) return true;
        if (val is JsonElement el)
        {
            return el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                   (el.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(el.GetString()));
        }
        return val is string s && string.IsNullOrWhiteSpace(s);
    }

    private static void EmitInvokedEvent(
        WorkflowInstance instance,
        string nodeId,
        string objectKey,
        int version,
        string methodName,
        string providerName,
        string status,
        string errorCategory,
        string? errorCode,
        long durationMs,
        int attempts,
        Dictionary<string, object?>? maskedInputs,
        Dictionary<string, object?>? maskedOutputs,
        IReadOnlyList<string> secretsToRedact)
    {
        string payloadJson = JsonSerializer.Serialize(new
        {
            smartObject = objectKey,
            version,
            smartObjectRef = version > 0 ? $"{objectKey}@{version}" : objectKey,
            method = methodName,
            provider = providerName,
            status,
            errorCategory,
            errorCode,
            durationMs,
            attempts,
            inputs = maskedInputs,
            outputs = maskedOutputs
        });

        payloadJson = RedactSecrets(payloadJson, secretsToRedact);
        instance.AddEvent("SmartObject.Invoked", nodeId, null, "system", payloadJson);
    }

    private static string RedactSecrets(string text, IReadOnlyList<string> secrets)
    {
        if (string.IsNullOrEmpty(text) || secrets.Count == 0) return text;
        string result = text;
        foreach (var secret in secrets)
        {
            if (!string.IsNullOrEmpty(secret))
            {
                result = result.Replace(secret, "***REDACTED***", StringComparison.Ordinal);
            }
        }
        return result;
    }

    public static List<SmartObjectMethodDto> DeserializeMethods(string? methodsJson)
    {
        if (string.IsNullOrWhiteSpace(methodsJson)) return new List<SmartObjectMethodDto>();
        try
        {
            return JsonSerializer.Deserialize<List<SmartObjectMethodDto>>(methodsJson) ?? new List<SmartObjectMethodDto>();
        }
        catch
        {
            return new List<SmartObjectMethodDto>();
        }
    }

    public static List<SmartObjectFieldDto> DeserializeFields(string? fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(fieldsJson)) return new List<SmartObjectFieldDto>();
        try
        {
            return JsonSerializer.Deserialize<List<SmartObjectFieldDto>>(fieldsJson) ?? new List<SmartObjectFieldDto>();
        }
        catch
        {
            return new List<SmartObjectFieldDto>();
        }
    }
}
