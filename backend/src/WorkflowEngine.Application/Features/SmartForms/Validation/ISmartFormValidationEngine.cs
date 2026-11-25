namespace WorkflowEngine.Application.Features.SmartForms.Validation;

public sealed record SmartFormValidationResult(
    bool IsValid,
    Dictionary<string, object?> SanitizedData,
    Dictionary<string, string[]> Errors,
    string? ErrorCode = null
);

/// <summary>
/// Deterministic server-side Zero-Trust validation engine for SmartForm submissions and task FormPatches.
/// Evaluates visibility rules, strips hidden/undeclared phantom fields, enforces readOnly immutability,
/// checks visible required fields, and validates data types and constraints without eval.
/// </summary>
public interface ISmartFormValidationEngine
{
    SmartFormValidationResult ValidateSubmission(
        string fieldsJson,
        IReadOnlyDictionary<string, object?>? submittedData);

    SmartFormValidationResult ValidatePatch(
        string fieldsJson,
        IReadOnlyDictionary<string, object?>? formPatch,
        IReadOnlyDictionary<string, object?>? existingData = null);
}
