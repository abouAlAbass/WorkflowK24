using System.Text.Json;
using WorkflowEngine.Application.Features.SmartObjects.Commands;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Domain.Enums;
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
                recordsCount: 3,
                version: 1,
                status: SmartObjectStatus.Published,
                methodsJson: JsonSerializer.Serialize(new List<SmartObjectMethodDto>
                {
                    new(
                        "GetById",
                        "read",
                        new List<SmartObjectMethodParameterDto> { new("DefinitionID", "number", true) },
                        new List<SmartObjectMethodParameterDto> { new("WorkflowKey", "string"), new("Name", "string") },
                        Idempotent: true,
                        Binding: new SmartObjectMethodBindingDto("InternalStore", RecordKeyInput: "DefinitionID"))
                }),
                publishedBy: "system"
            );

            await repository.AddAsync(defaultSo, ct);
            await repository.AddRecordAsync(new Domain.Entities.SmartObjectRecordEntity("so-sql-schema", "1", "{\"DefinitionID\":1,\"WorkflowKey\":\"employee-onboarding\",\"Name\":\"Employee Onboarding Process\"}"), ct);
            await repository.AddRecordAsync(new Domain.Entities.SmartObjectRecordEntity("so-sql-schema", "2", "{\"DefinitionID\":2,\"WorkflowKey\":\"expense-approval\",\"Name\":\"Purchase Request Approval\"}"), ct);
            await repository.AddRecordAsync(new Domain.Entities.SmartObjectRecordEntity("so-sql-schema", "3", "{\"DefinitionID\":3,\"WorkflowKey\":\"it-access\",\"Name\":\"IT System Access Request\"}"), ct);

            entities = await repository.GetAllAsync(ct);
        }

        return entities.Select(e => CreateSmartObjectHandler.MapToDto(e)).ToList();
    }
}
