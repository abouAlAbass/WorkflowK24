using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Application.Features.Tasks.Queries;

public record GetTasksByAssigneeQuery(string AssigneeId, TaskStatus? Status = null);
public record GetTasksByInstanceIdQuery(string InstanceId);
public record GetTaskByIdQuery(string TaskId);
