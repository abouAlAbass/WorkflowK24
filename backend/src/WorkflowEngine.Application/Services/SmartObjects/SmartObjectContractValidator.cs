using System.Text.Json;
using System.Text.RegularExpressions;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Application.Services.Executors;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Services.SmartObjects;

public interface ISmartObjectContractValidator
{
    Task ValidateGraphAsync(string graphJson, CancellationToken ct = default);
}

/// <summary>
/// Performs static contract validation of all SmartObject nodes in a workflow graph at publish time.
/// Verifies that each node references an existing Published SmartObject@Version, a declared method,
/// required inputs, and type-compatible input/output mappings.
/// </summary>
public class SmartObjectContractValidator : ISmartObjectContractValidator
{
    private static readonly Regex VarTemplateRegex = new(@"^\{\{\s*vars\.([a-zA-Z0-9_\-]+)\s*\}\}$", RegexOptions.Compiled);

    private readonly ISmartObjectRepository _smartObjectRepository;

    public SmartObjectContractValidator(ISmartObjectRepository smartObjectRepository)
    {
        _smartObjectRepository = smartObjectRepository ?? throw new ArgumentNullException(nameof(smartObjectRepository));
    }

    public async Task ValidateGraphAsync(string graphJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
        {
            return;
        }

        ReactFlowGraph? graph;
        try
        {
            graph = JsonSerializer.Deserialize<ReactFlowGraph>(
                graphJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return;
        }

        if (graph?.Nodes == null || graph.Nodes.Count == 0)
        {
            return;
        }

        var errors = new List<string>();
        var declaredVars = CollectDeclaredWorkflowVariables(graph.Nodes);
        var smartObjectNodes = CollectSmartObjectNodes(graph.Nodes);

        foreach (var node in smartObjectNodes)
        {
            await ValidateNodeAsync(node, declaredVars, errors, ct);
        }

        if (errors.Count > 0)
        {
            throw new WorkflowContractValidationException(
                $"Workflow definition failed SmartObject contract validation with {errors.Count} error(s).",
                errors);
        }
    }

    private async Task ValidateNodeAsync(
        ReactFlowNode node,
        Dictionary<string, string> declaredVars,
        List<string> errors,
        CancellationToken ct)
    {
        var soData = node.Data.SmartObjectData;
        if (soData == null)
        {
            errors.Add($"Node '{node.Id}': missing 'smartObjectData' configuration.");
            return;
        }

        if (!SmartObjectNodeExecutor.TryParseSmartObjectRef(soData.SmartObjectRef, out string objectKey, out int version))
        {
            errors.Add($"Node '{node.Id}': invalid smartObjectRef '{soData.SmartObjectRef}'. Expected 'ObjectKey@Version' (e.g. 'Customer@2').");
            return;
        }

        if (string.IsNullOrWhiteSpace(soData.MethodName))
        {
            errors.Add($"Node '{node.Id}': missing 'methodName' for SmartObject '{soData.SmartObjectRef}'.");
            return;
        }

        var smartObject = await _smartObjectRepository.GetByNameAndVersionAsync(objectKey, version, ct);
        if (smartObject == null)
        {
            errors.Add($"Node '{node.Id}': SmartObject '{objectKey}@{version}' does not exist.");
            return;
        }

        if (smartObject.Status != SmartObjectStatus.Published)
        {
            errors.Add($"Node '{node.Id}': SmartObject '{smartObject.ObjectRef}' has status '{smartObject.Status}' and must be Published before a workflow can bind to it.");
            return;
        }

        var methods = SmartObjectNodeExecutor.DeserializeMethods(smartObject.MethodsJson);
        var method = methods.FirstOrDefault(m => string.Equals(m.Name, soData.MethodName, StringComparison.OrdinalIgnoreCase));
        if (method == null)
        {
            errors.Add($"Node '{node.Id}': method '{soData.MethodName}' does not exist on SmartObject '{smartObject.ObjectRef}'.");
            return;
        }

        var declaredInputs = (method.Inputs ?? new List<SmartObjectMethodParameterDto>())
            .ToDictionary(i => i.Name, i => i, StringComparer.OrdinalIgnoreCase);

        var inputMapping = soData.InputMapping ?? new Dictionary<string, JsonElement>();

        // 1. Verify all required inputs are provided
        foreach (var req in declaredInputs.Values.Where(i => i.Required))
        {
            bool provided = inputMapping.Keys.Any(k => string.Equals(k, req.Name, StringComparison.OrdinalIgnoreCase));
            if (!provided)
            {
                errors.Add($"Node '{node.Id}': missing required input '{req.Name}' for '{smartObject.ObjectRef}.{method.Name}'.");
            }
        }

        // 2. Verify supplied inputs exist in contract and have compatible types
        foreach (var kv in inputMapping)
        {
            if (declaredInputs.Count > 0 && !declaredInputs.TryGetValue(kv.Key, out var paramDef))
            {
                errors.Add($"Node '{node.Id}': unknown input '{kv.Key}' is not declared on '{smartObject.ObjectRef}.{method.Name}'.");
                continue;
            }

            if (declaredInputs.TryGetValue(kv.Key, out var param))
            {
                ValidateInputValueType(node.Id, smartObject.ObjectRef, method.Name, param, kv.Value, declaredVars, errors);
            }
        }

        // 3. Verify outputMapping references valid declared outputs and type-compatible target variables
        var declaredOutputs = (method.Outputs ?? new List<SmartObjectMethodParameterDto>())
            .ToDictionary(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase);

        if (soData.OutputMapping != null && declaredOutputs.Count > 0)
        {
            foreach (var kv in soData.OutputMapping)
            {
                string targetVar = kv.Key.Trim();
                string sourceOutput = kv.Value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sourceOutput) || sourceOutput.StartsWith("$", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!declaredOutputs.TryGetValue(sourceOutput, out var outParam))
                {
                    errors.Add($"Node '{node.Id}': output '{sourceOutput}' is not declared on '{smartObject.ObjectRef}.{method.Name}'.");
                    continue;
                }

                if (declaredVars.TryGetValue(targetVar, out var targetVarType) &&
                    !AreTypesCompatible(outParam.Type, targetVarType))
                {
                    errors.Add($"Node '{node.Id}': output '{outParam.Name}' ({outParam.Type}) is incompatible with workflow variable '{targetVar}' ({targetVarType}).");
                }
            }
        }
    }

    private static void ValidateInputValueType(
        string nodeId,
        string objectRef,
        string methodName,
        SmartObjectMethodParameterDto param,
        JsonElement value,
        Dictionary<string, string> declaredVars,
        List<string> errors)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            if (param.Required)
            {
                errors.Add($"Node '{nodeId}': required input '{param.Name}' for '{objectRef}.{methodName}' cannot be null.");
            }
            return;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            string strVal = value.GetString() ?? string.Empty;
            if (strVal.Contains("{{", StringComparison.Ordinal))
            {
                // If it's a direct {{vars.x}} reference and x is declared on Trigger, verify type compatibility
                var match = VarTemplateRegex.Match(strVal.Trim());
                if (match.Success && declaredVars.TryGetValue(match.Groups[1].Value, out var varType))
                {
                    if (!AreTypesCompatible(varType, param.Type))
                    {
                        errors.Add($"Node '{nodeId}': input '{param.Name}' expects '{param.Type}' but variable '{match.Groups[1].Value}' is '{varType}'.");
                    }
                }
                return;
            }
        }

        string expected = NormalizeType(param.Type);
        bool validLiteral = expected switch
        {
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "string" => value.ValueKind == JsonValueKind.String,
            "date" => value.ValueKind == JsonValueKind.String,
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            _ => true
        };

        if (!validLiteral)
        {
            errors.Add($"Node '{nodeId}': input '{param.Name}' for '{objectRef}.{methodName}' expects type '{param.Type}' but got JSON {value.ValueKind}.");
        }
    }

    private static bool AreTypesCompatible(string sourceType, string targetType)
    {
        string s = NormalizeType(sourceType);
        string t = NormalizeType(targetType);
        if (s == "any" || t == "any") return true;
        if (s == t) return true;
        // String can accept date/guid; date can accept string
        if ((s == "date" && t == "string") || (s == "string" && t == "date")) return true;
        return false;
    }

    private static string NormalizeType(string? raw)
    {
        return (raw ?? "any").Trim().ToLowerInvariant() switch
        {
            "string" or "text" or "guid" or "uuid" or "email" => "string",
            "number" or "int" or "integer" or "decimal" or "double" or "float" or "long" or "currency" => "number",
            "bool" or "boolean" => "boolean",
            "date" or "datetime" or "timestamp" => "date",
            "object" or "json" => "object",
            "array" or "list" => "array",
            _ => "any"
        };
    }

    private static Dictionary<string, string> CollectDeclaredWorkflowVariables(List<ReactFlowNode> nodes)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            if (node.Data.TriggerData?.Variables != null)
            {
                foreach (var v in node.Data.TriggerData.Variables)
                {
                    if (!string.IsNullOrWhiteSpace(v.Name))
                    {
                        map[v.Name.Trim()] = v.Type;
                    }
                }
            }
        }
        return map;
    }

    private static List<ReactFlowNode> CollectSmartObjectNodes(List<ReactFlowNode> nodes)
    {
        var result = new List<ReactFlowNode>();
        foreach (var node in nodes)
        {
            string type = (node.Data.NodeType ?? string.Empty).Trim().ToLowerInvariant();
            if (type is "smartobject" or "smart_object")
            {
                result.Add(node);
            }

            if (node.Data.StageData?.SubWorkflow?.Nodes is { Count: > 0 } subNodes)
            {
                result.AddRange(CollectSmartObjectNodes(subNodes));
            }
        }
        return result;
    }
}
