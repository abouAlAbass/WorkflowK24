using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class WorkflowDefinitionRepository : IWorkflowDefinitionRepository
{
    private readonly WorkflowEngineDbContext _dbContext;

    public WorkflowDefinitionRepository(WorkflowEngineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<WorkflowDefinition?> GetByIdAsync(int definitionId, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowDefinitions
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.DefinitionId == definitionId, ct);
    }

    public async Task<WorkflowDefinition?> GetByKeyAsync(string workflowKey, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowDefinitions
            .FirstOrDefaultAsync(d => d.WorkflowKey == workflowKey, ct);
    }

    public async Task<WorkflowDefinition?> GetByKeyWithVersionsAsync(string workflowKey, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowDefinitions
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.WorkflowKey == workflowKey, ct);
    }

    public async Task<WorkflowVersion?> GetActiveVersionByKeyAsync(string workflowKey, CancellationToken ct = default)
    {
        return await _dbContext.WorkflowVersions
            .Include(v => v.Definition)
            .FirstOrDefaultAsync(v => v.Definition.WorkflowKey == workflowKey && v.IsActive, ct);
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.WorkflowDefinitions
            .Include(d => d.Versions)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
    }

    public void Add(WorkflowDefinition definition)
    {
        _dbContext.WorkflowDefinitions.Add(definition);
    }

    public void Update(WorkflowDefinition definition)
    {
        _dbContext.WorkflowDefinitions.Update(definition);
    }

    public async Task<bool> DeleteByKeyAsync(string workflowKey, CancellationToken ct = default)
    {
        var definition = await _dbContext.WorkflowDefinitions
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.WorkflowKey == workflowKey, ct);

        if (definition == null)
        {
            return false;
        }

        var versionIds = definition.Versions.Select(v => v.VersionId).ToList();
        if (versionIds.Count > 0)
        {
            var instances = await _dbContext.WorkflowInstances
                .Where(i => versionIds.Contains(i.VersionId))
                .ToListAsync(ct);

            if (instances.Count > 0)
            {
                _dbContext.WorkflowInstances.RemoveRange(instances);
            }
        }

        _dbContext.WorkflowDefinitions.Remove(definition);
        return true;
    }
}
