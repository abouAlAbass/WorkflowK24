using WorkflowEngine.Application.Features.Definitions.Commands;
using WorkflowEngine.Application.Features.Definitions.DTOs;
using WorkflowEngine.Application.Features.Definitions.Queries;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Definitions.Handlers;

/// <summary>
/// Wolverine CQRS handlers for workflow definition operations.
/// </summary>
public class WorkflowDefinitionHandlers
{
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowDefinitionHandlers(
        IWorkflowDefinitionRepository definitionRepository,
        IUnitOfWork unitOfWork)
    {
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WorkflowDefinitionCreatedResult> Handle(CreateWorkflowDefinitionCommand command, CancellationToken ct)
    {
        var definition = await _definitionRepository.GetByKeyWithVersionsAsync(command.WorkflowKey, ct);

        if (definition == null)
        {
            definition = new WorkflowDefinition(
                command.WorkflowKey,
                command.Name,
                command.CreatedBy,
                command.Description,
                command.Category);

            _definitionRepository.Add(definition);
        }

        var version = definition.AddVersion(
            command.GraphJson,
            command.CreatedBy,
            "Version published via Visual Designer",
            setActive: true);

        await _unitOfWork.SaveChangesAsync(ct);

        return new WorkflowDefinitionCreatedResult(
            definition.DefinitionId,
            version.VersionId,
            definition.WorkflowKey,
            version.VersionNumber);
    }

    public async Task<IReadOnlyList<WorkflowDefinitionSummaryDto>> Handle(GetWorkflowDefinitionsQuery query, CancellationToken ct)
    {
        var definitions = await _definitionRepository.GetAllAsync(ct);

        return definitions.Select(d =>
        {
            var activeVersion = d.Versions.FirstOrDefault(v => v.IsActive) ?? d.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            return new WorkflowDefinitionSummaryDto(
                d.DefinitionId,
                d.WorkflowKey,
                d.Name,
                d.Description,
                d.Category,
                d.CreatedBy,
                d.CreatedAt,
                d.UpdatedAt,
                activeVersion?.VersionNumber ?? 0,
                activeVersion?.GraphJson);
        }).ToList();
    }

    public async Task<WorkflowDefinitionSummaryDto?> Handle(GetWorkflowDefinitionByKeyQuery query, CancellationToken ct)
    {
        var definition = await _definitionRepository.GetByKeyWithVersionsAsync(query.WorkflowKey, ct);
        if (definition == null) return null;

        var activeVersion = definition.Versions.FirstOrDefault(v => v.IsActive) ?? definition.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        return new WorkflowDefinitionSummaryDto(
            definition.DefinitionId,
            definition.WorkflowKey,
            definition.Name,
            definition.Description,
            definition.Category,
            definition.CreatedBy,
            definition.CreatedAt,
            definition.UpdatedAt,
            activeVersion?.VersionNumber ?? 0,
            activeVersion?.GraphJson);
    }

    public async Task<bool> Handle(DeleteWorkflowDefinitionCommand command, CancellationToken ct)
    {
        bool deleted = await _definitionRepository.DeleteByKeyAsync(command.WorkflowKey, ct);
        if (deleted)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return deleted;
    }
}
