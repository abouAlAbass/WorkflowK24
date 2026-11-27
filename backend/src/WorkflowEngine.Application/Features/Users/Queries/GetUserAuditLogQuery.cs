using WorkflowEngine.Application.Features.Users.DTOs;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Users.Queries;

public record GetUserAuditLogQuery(int Limit = 100);

public static class GetUserAuditLogHandler
{
    public static async Task<IReadOnlyList<UserAuditDto>> Handle(
        GetUserAuditLogQuery query,
        IUserAuditRepository repository,
        CancellationToken ct)
    {
        var entries = await repository.GetRecentAsync(query.Limit, ct);
        return entries.Select(a => new UserAuditDto(
            a.Id, a.ActorId, a.ActorEmail, a.Action, a.TargetUserId, a.TargetEmail, a.Details, a.OccurredAt)).ToList();
    }
}
