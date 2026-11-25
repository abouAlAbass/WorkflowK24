using System.Text.Json.Serialization;

namespace WorkflowEngine.Application.Features.SmartForms.DTOs;

public record SmartFormVisibilityRuleDto(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("operator")] string Operator,
    [property: JsonPropertyName("value")] string Value
);

public record SmartFormRepeaterColumnDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("type")] string Type = "text",
    [property: JsonPropertyName("required")] bool Required = false
);

public record SmartFormFieldDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("required")] bool Required = false,
    [property: JsonPropertyName("readOnly")] bool ReadOnly = false,
    [property: JsonPropertyName("width")] string Width = "full",
    [property: JsonPropertyName("placeholder")] string? Placeholder = null,
    [property: JsonPropertyName("helpText")] string? HelpText = null,
    [property: JsonPropertyName("defaultValue")] string? DefaultValue = null,
    [property: JsonPropertyName("options")] List<string>? Options = null,
    [property: JsonPropertyName("lookupSmartObjectId")] string? LookupSmartObjectId = null,
    [property: JsonPropertyName("lookupDisplayField")] string? LookupDisplayField = null,
    [property: JsonPropertyName("lookupValueField")] string? LookupValueField = null,
    [property: JsonPropertyName("repeaterColumns")] List<SmartFormRepeaterColumnDto>? RepeaterColumns = null,
    [property: JsonPropertyName("visibilityRule")] SmartFormVisibilityRuleDto? VisibilityRule = null
);

public record SmartFormDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("linkedSmartObjectId")] string? LinkedSmartObjectId,
    [property: JsonPropertyName("linkedWorkflowKey")] string? LinkedWorkflowKey,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("submissionsCount")] int SubmissionsCount,
    [property: JsonPropertyName("fields")] List<SmartFormFieldDto> Fields,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt,
    [property: JsonPropertyName("version")] int Version = 1
);

public record SmartFormSubmissionDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("smartFormId")] string SmartFormId,
    [property: JsonPropertyName("submittedBy")] string SubmittedBy,
    [property: JsonPropertyName("data")] Dictionary<string, object?> Data,
    [property: JsonPropertyName("linkedInstanceId")] string? LinkedInstanceId,
    [property: JsonPropertyName("submittedAt")] DateTime SubmittedAt,
    [property: JsonPropertyName("idempotentReplay")] bool IdempotentReplay = false
);

public record FormAttachmentDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes,
    [property: JsonPropertyName("sha256Hash")] string Sha256Hash,
    [property: JsonPropertyName("uploadedBy")] string UploadedBy,
    [property: JsonPropertyName("uploadedAt")] DateTime UploadedAt,
    [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
    [property: JsonPropertyName("uploadedByUserId")] string? UploadedByUserId = null,
    [property: JsonPropertyName("workflowInstanceId")] string? WorkflowInstanceId = null,
    [property: JsonPropertyName("submissionId")] string? SubmissionId = null
);
