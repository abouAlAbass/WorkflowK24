using System.Text.Json;
using System.Text.Json.Serialization;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.SmartObjects.Queries;

public record SmartObjectLookupPageDto(
    [property: JsonPropertyName("items")] IReadOnlyList<Dictionary<string, object?>> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages,
    [property: JsonPropertyName("hasMore")] bool HasMore
);

public record LookupSmartObjectRecordsQuery(
    string SmartObjectId,
    string? Query = null,
    int Page = 1,
    int PageSize = 25
);

public static class LookupSmartObjectRecordsHandler
{
    public static async Task<SmartObjectLookupPageDto?> Handle(
        LookupSmartObjectRecordsQuery query,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        var smartObject = await repository.GetByIdAsync(query.SmartObjectId, ct);
        if (smartObject == null)
        {
            return null;
        }

        int safePage = Math.Max(1, query.Page);
        int safePageSize = Math.Clamp(query.PageSize, 1, 100);

        var (records, totalCount) = await repository.SearchRecordsAsync(
            query.SmartObjectId,
            query.Query,
            safePage,
            safePageSize,
            ct);

        var items = new List<Dictionary<string, object?>>(records.Count);
        foreach (var r in records)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(r.DataJson);
                if (dict != null)
                {
                    items.Add(dict);
                }
            }
            catch
            {
                // Ignore malformed JSON rows
            }
        }

        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)safePageSize);
        bool hasMore = safePage < totalPages;

        return new SmartObjectLookupPageDto(
            Items: items,
            Page: safePage,
            PageSize: safePageSize,
            TotalCount: totalCount,
            TotalPages: totalPages,
            HasMore: hasMore);
    }
}
