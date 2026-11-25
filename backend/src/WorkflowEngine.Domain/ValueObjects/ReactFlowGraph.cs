using System.Text.Json.Serialization;

namespace WorkflowEngine.Domain.ValueObjects;

/// <summary>
/// Strongly-typed AST representing the serialized React Flow visual graph.
/// </summary>
public record ReactFlowGraph(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("nodes")] List<ReactFlowNode> Nodes,
    [property: JsonPropertyName("edges")] List<ReactFlowEdge> Edges
);

public record ReactFlowNode(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("position")] NodePosition Position,
    [property: JsonPropertyName("data")] ReactFlowNodeData Data
);

public record NodePosition(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y
);

public record ReactFlowNodeData(
    [property: JsonPropertyName("nodeType")] string NodeType,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("triggerData")] TriggerDataPayload? TriggerData = null,
    [property: JsonPropertyName("approvalData")] ApprovalDataPayload? ApprovalData = null,
    [property: JsonPropertyName("sendEmailData")] SendEmailDataPayload? SendEmailData = null,
    [property: JsonPropertyName("endData")] EndDataPayload? EndData = null,
    [property: JsonPropertyName("decisionData")] DecisionDataPayload? DecisionData = null,
    [property: JsonPropertyName("stageData")] StageDataPayload? StageData = null
);

public record StageDataPayload(
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("stageName")] string StageName = "",
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("stageColor")] string? StageColor = null,
    [property: JsonPropertyName("subWorkflow")] StageSubWorkflowPayload? SubWorkflow = null
);

public record StageSubWorkflowPayload(
    [property: JsonPropertyName("nodes")] List<ReactFlowNode>? Nodes = null,
    [property: JsonPropertyName("edges")] List<ReactFlowEdge>? Edges = null
);

public record TriggerDataPayload(
    [property: JsonPropertyName("triggerType")] string TriggerType,
    [property: JsonPropertyName("eventName")] string EventName,
    [property: JsonPropertyName("variables")] List<WorkflowVariableDto>? Variables = null,
    [property: JsonPropertyName("formId")] string? FormId = null
);

public record WorkflowVariableDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("defaultValue")] string? DefaultValue = null,
    [property: JsonPropertyName("isSensitive")] bool IsSensitive = false
);

public record ApprovalDataPayload(
    [property: JsonPropertyName("assignmentType")] string AssignmentType,
    [property: JsonPropertyName("assignee")] string Assignee,
    [property: JsonPropertyName("outcomes")] List<ApprovalOutcomeDto> Outcomes,
    [property: JsonPropertyName("allowReassignment")] bool? AllowReassignment = true,
    [property: JsonPropertyName("dueDateDays")] int? DueDateDays = 2,
    [property: JsonPropertyName("formId")] string? FormId = null,
    [property: JsonPropertyName("formMode")] string? FormMode = "readonly"
);

public record ApprovalOutcomeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("color")] string Color
);

public record SendEmailDataPayload(
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("isHtml")] bool? IsHtml = false
);

public record EndDataPayload(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message
);

public record DecisionDataPayload(
    [property: JsonPropertyName("conditions")] List<DecisionConditionDto> Conditions,
    [property: JsonPropertyName("trueOutcomeLabel")] string TrueOutcomeLabel,
    [property: JsonPropertyName("falseOutcomeLabel")] string FalseOutcomeLabel,
    [property: JsonPropertyName("logicalOperator")] string? LogicalOperator = "AND"
);

public record DecisionConditionDto(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("operator")] string Operator,
    [property: JsonPropertyName("value")] string Value
);

public record ReactFlowEdge(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("sourceHandle")] string? SourceHandle = null,
    [property: JsonPropertyName("targetHandle")] string? TargetHandle = null,
    [property: JsonPropertyName("label")] string? Label = null
);
