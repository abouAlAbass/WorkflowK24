using System.Text.Json;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.SmartObjects.Queries;

public record GetSmartObjectRecordsQuery(string SmartObjectId);

public static class GetSmartObjectRecordsHandler
{
    public static async Task<IReadOnlyList<Dictionary<string, object?>>> Handle(
        GetSmartObjectRecordsQuery query,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        var records = await repository.GetRecordsAsync(query.SmartObjectId, ct);

        var list = new List<Dictionary<string, object?>>();
        foreach (var r in records)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(r.DataJson);
                if (dict != null)
                {
                    list.Add(dict);
                }
            }
            catch
            {
                // ignore malformed rows
            }
        }

        return list;
    }
}
