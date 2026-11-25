using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Domain.Repositories;

/// <summary>
/// Port for retrieving and managing workflow tasks.
/// </summary>
public interface IWorkflowTaskRepository
{
    Task<WorkflowTask?> GetByIdAsync(string taskId, CancellationToken ct = default);
    Task<WorkflowTask?> GetByIdWithInstanceAsync(string taskId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTask>> GetAccessibleTasksAsync(
        string currentUserId,
        IReadOnlyList<string> userGroups,
        bool isAdmin,
        TaskStatus? status = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTask>> GetByAssigneeAsync(string assigneeId, TaskStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTask>> GetPendingTasksForInstanceAsync(string instanceId, CancellationToken ct = default);
    void Add(WorkflowTask task);
    void Update(WorkflowTask task);
}
