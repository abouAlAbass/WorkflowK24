using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class WorkflowTaskRepository : IWorkflowTaskRepository
{
    private readonly WorkflowEngineDbContext _dbContext;

    public WorkflowTaskRepository(WorkflowEngineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<WorkflowTask?> GetByIdAsync(string taskId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.WorkflowVersion)
                    .ThenInclude(v => v.Definition)
            .Include(t => t.Slots)
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.TaskId == taskId, ct);
    }

    public async Task<WorkflowTask?> GetByIdWithInstanceAsync(string taskId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.WorkflowVersion)
                    .ThenInclude(v => v.Definition)
            .Include(t => t.Instance)
                .ThenInclude(i => i.AuditLogs)
            .Include(t => t.Instance)
                .ThenInclude(i => i.Tasks)
            .Include(t => t.Instance)
                .ThenInclude(i => i.Events)
            .Include(t => t.Instance)
                .ThenInclude(i => i.NodeExecutions)
            .Include(t => t.Instance)
                .ThenInclude(i => i.Tokens)
            .Include(t => t.Instance)
                .ThenInclude(i => i.JoinStates)
            .Include(t => t.Slots)
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.TaskId == taskId, ct);
    }

    public async Task<IReadOnlyList<WorkflowTask>> GetAccessibleTasksAsync(
        string currentUserId,
        IReadOnlyList<string> userGroups,
        bool isAdmin,
        TaskStatus? status = null,
        CancellationToken ct = default,
        IReadOnlyList<string>? userRoles = null)
    {
        var query = _dbContext.WorkflowTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.WorkflowVersion)
                    .ThenInclude(v => v.Definition)
            .Include(t => t.Slots)
            .AsQueryable();

        if (!isAdmin)
        {
            string normalizedUser = (currentUserId ?? string.Empty).Trim().ToLower();
            var normalizedGroups = (userGroups ?? Array.Empty<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.Trim().ToLower())
                .ToList();
            var normalizedRoles = (userRoles ?? Array.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim().ToLower())
                .ToList();

            query = query.Where(t =>
                (t.AssigneeType == AssigneeType.User && t.AssigneeId.ToLower() == normalizedUser) ||
                (t.AssigneeType == AssigneeType.Group && normalizedGroups.Contains(t.AssigneeId.ToLower())) ||
                (t.AssigneeType == AssigneeType.Role && normalizedRoles.Contains(t.AssigneeId.ToLower())) ||
                t.OriginalAssigneeId.ToLower() == normalizedUser);
        }

        if (status.HasValue)
        {
            if (status.Value == TaskStatus.Pending)
            {
                query = query.Where(t => t.Status == TaskStatus.Pending || t.Status == TaskStatus.Delegated);
            }
            else
            {
                query = query.Where(t => t.Status == status.Value);
            }
        }

        return await query.OrderByDescending(t => t.AssignedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowTask>> GetByAssigneeAsync(string assigneeId, TaskStatus? status = null, CancellationToken ct = default)
    {
        var query = _dbContext.WorkflowTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.WorkflowVersion)
                    .ThenInclude(v => v.Definition)
            .Include(t => t.Slots)
            .Where(t => t.AssigneeId == assigneeId);

        if (status.HasValue)
        {
            if (status.Value == TaskStatus.Pending)
            {
                query = query.Where(t => t.Status == TaskStatus.Pending || t.Status == TaskStatus.Delegated);
            }
            else
            {
                query = query.Where(t => t.Status == status.Value);
            }
        }

        return await query.OrderByDescending(t => t.AssignedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowTask>> GetPendingTasksForInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.WorkflowVersion)
                    .ThenInclude(v => v.Definition)
            .Include(t => t.Slots)
            .Where(t => t.InstanceId == instanceId && (t.Status == TaskStatus.Pending || t.Status == TaskStatus.Delegated))
            .OrderBy(t => t.AssignedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskAssignment>> GetAssignmentsAsync(string taskId, CancellationToken ct = default)
    {
        return await _dbContext.TaskAssignments
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowTask>> GetOverdueTasksAsync(DateTime utcNow, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.WorkflowVersion)
                    .ThenInclude(v => v.Definition)
            .Include(t => t.Instance)
                .ThenInclude(i => i.Events)
            .Include(t => t.Assignments)
            .Where(t =>
                (t.Status == TaskStatus.Pending || t.Status == TaskStatus.Delegated) &&
                t.EscalatedAt == null &&
                ((t.DueAt != null && t.DueAt < utcNow) || (t.DueAt == null && t.DueDate != null && t.DueDate < utcNow)))
            .ToListAsync(ct);
    }

    public void Add(WorkflowTask task)
    {
        _dbContext.WorkflowTasks.Add(task);
    }

    public void Update(WorkflowTask task)
    {
        _dbContext.WorkflowTasks.Update(task);
    }
}
