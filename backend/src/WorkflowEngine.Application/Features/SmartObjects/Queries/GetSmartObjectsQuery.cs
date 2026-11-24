using System.Text.Json;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.SmartObjects.Queries;

public record GetSmartObjectsQuery;

public static class GetSmartObjectsHandler
{
    public static async Task<IReadOnlyList<SmartObjectDto>> Handle(
        GetSmartObjectsQuery query,
        ISmartObjectRepository repository,
        CancellationToken ct)
    {
        var entities = await repository.GetAllAsync(ct);

        // If repository is empty (e.g. initial setup), seed default enterprise smart object
        if (entities.Count == 0)
        {
            var defaultSo = new Domain.Entities.SmartObjectEntity(
                "so-sql-schema",
                "WorkflowDefinitions",
                "SQL Server / PostgreSQL Schema",
                "BPM relational enterprise master registry.",
                "Core",
                JsonSerializer.Serialize(new List<SmartObjectFieldDto>
                {
                    new("DefinitionID", "Number", true, true),
                    new("WorkflowKey", "String", false, true),
                    new("Name", "String", false, true)
                }),
                recordsCount: 3
            );

            await repository.AddAsync(defaultSo, ct);
            await repository.AddRecordAsync(new Domain.Entities.SmartObjectRecordEntity("so-sql-schema", "1", "{\"DefinitionID\":1,\"WorkflowKey\":\"employee-onboarding\",\"Name\":\"Employee Onboarding Process\"}"), ct);
            await repository.AddRecordAsync(new Domain.Entities.SmartObjectRecordEntity("so-sql-schema", "2", "{\"DefinitionID\":2,\"WorkflowKey\":\"expense-approval\",\"Name\":\"Purchase Request Approval\"}"), ct);
            await repository.AddRecordAsync(new Domain.Entities.SmartObjectRecordEntity("so-sql-schema", "3", "{\"DefinitionID\":3,\"WorkflowKey\":\"it-access\",\"Name\":\"IT System Access Request\"}"), ct);

            entities = await repository.GetAllAsync(ct);
        }

        return entities.Select(e =>
        {
            var fields = new List<SmartObjectFieldDto>();
            try
            {
                fields = JsonSerializer.Deserialize<List<SmartObjectFieldDto>>(e.FieldsJson) ?? new();
            }
            catch
            {
                // ignore
            }

            return new SmartObjectDto(
                e.Id,
                e.Name,
                e.DisplayName,
                e.Description,
                e.Category,
                e.RecordsCount,
                fields
            );
        }).ToList();
    }
}
