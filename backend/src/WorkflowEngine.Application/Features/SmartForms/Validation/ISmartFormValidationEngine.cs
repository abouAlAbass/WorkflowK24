namespace WorkflowEngine.Application.Features.SmartForms.Validation;

public sealed record SmartFormValidationResult(
    bool IsValid,
    Dictionary<string, object?> SanitizedData,
    Dictionary<string, string[]> Errors,
    string? ErrorCode = null
);

/// <summary>
/// Deterministic server-side Zero-Trust validation engine for SmartForm submissions and task FormPatches.
/// Evaluates legacy visibility rules and JSONLogic FormRule[] (visible, required, readOnly, computedValue)
/// in DAG topological order, strips hidden/undeclared phantom fields, recomputes calculated fields server-side,
/// enforces static and conditional readOnly immutability, checks required fields, and validates data types.
/// </summary>
public interface ISmartFormValidationEngine
{
    SmartFormValidationResult ValidateSubmission(
        string fieldsJson,
        IReadOnlyDictionary<string, object?>? submittedData,
        string? rulesJson = null);

    SmartFormValidationResult ValidatePatch(
        string fieldsJson,
        IReadOnlyDictionary<string, object?>? formPatch,
        IReadOnlyDictionary<string, object?>? existingData = null,
        string? rulesJson = null);
}
