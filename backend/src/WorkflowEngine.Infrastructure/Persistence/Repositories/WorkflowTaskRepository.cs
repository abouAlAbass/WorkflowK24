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
            .FirstOrDefaultAsync(t => t.TaskId == taskId, ct);
    }

    public async Task<IReadOnlyList<WorkflowTask>> GetAccessibleTasksAsync(
        string currentUserId,
        IReadOnlyList<string> userGroups,
        bool isAdmin,
        TaskStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.WorkflowTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.WorkflowVersion)
                    .ThenInclude(v => v.Definition)
            .AsQueryable();

        if (!isAdmin)
        {
            string normalizedUser = (currentUserId ?? string.Empty).Trim().ToLower();
            var normalizedGroups = (userGroups ?? Array.Empty<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.Trim().ToLower())
                .ToList();

            query = query.Where(t =>
                (t.AssigneeType == AssigneeType.User && t.AssigneeId.ToLower() == normalizedUser) ||
                (t.AssigneeType == AssigneeType.Group && normalizedGroups.Contains(t.AssigneeId.ToLower())));
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return await query.OrderByDescending(t => t.AssignedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowTask>> GetByAssigneeAsync(string assigneeId, TaskStatus? status = null, CancellationToken ct = default)
    {
        var query = _dbContext.WorkflowTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.WorkflowVersion)
                    .ThenInclude(v => v.Definition)
            .Where(t => t.AssigneeId == assigneeId);

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return await query.OrderByDescending(t => t.AssignedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowTask>> GetPendingTasksForInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.WorkflowVersion)
                    .ThenInclude(v => v.Definition)
            .Where(t => t.InstanceId == instanceId && t.Status == TaskStatus.Pending)
            .OrderBy(t => t.AssignedAt)
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
