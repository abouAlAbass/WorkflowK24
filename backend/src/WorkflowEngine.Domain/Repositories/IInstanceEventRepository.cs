using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IInstanceEventRepository
{
    Task<IReadOnlyList<InstanceEvent>> GetByInstanceIdAsync(string instanceId, CancellationToken ct = default);
    Task<IReadOnlyList<InstanceEvent>> GetByInstanceIdCursorAsync(string instanceId, int afterSeq, int limit, CancellationToken ct = default);
    Task<int> GetNextSequenceAsync(string instanceId, CancellationToken ct = default);
    void Add(InstanceEvent instanceEvent);
}
