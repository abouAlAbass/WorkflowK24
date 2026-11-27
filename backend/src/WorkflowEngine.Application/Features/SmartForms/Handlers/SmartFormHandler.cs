using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Common.Security;
using WorkflowEngine.Application.Features.SmartForms.DTOs;
using WorkflowEngine.Application.Features.SmartForms.Validation;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.SmartForms.Handlers;

public record CreateSmartFormCommand(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("linkedSmartObjectId")] string? LinkedSmartObjectId,
    [property: JsonPropertyName("linkedWorkflowKey")] string? LinkedWorkflowKey,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("fields")] List<SmartFormFieldDto>? Fields,
    [property: JsonPropertyName("expectedVersion")] int? ExpectedVersion = null,
    [property: JsonPropertyName("publishImmediately")] bool PublishImmediately = false,
    [property: JsonIgnore] string? PublishedBy = null,
    [property: JsonPropertyName("rules")] List<FormRuleDto>? Rules = null
);

public record UpdateSmartFormCommand(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("linkedSmartObjectId")] string? LinkedSmartObjectId,
    [property: JsonPropertyName("linkedWorkflowKey")] string? LinkedWorkflowKey,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("fields")] List<SmartFormFieldDto>? Fields,
    [property: JsonPropertyName("expectedVersion")] int? ExpectedVersion = null,
    [property: JsonPropertyName("rules")] List<FormRuleDto>? Rules = null
);

public record PublishSmartFormCommand(string Id, string PublishedBy);
public record GetSmartFormVersionsQuery(string SmartFormId);
public record GetSmartFormVersionByIdQuery(string SmartFormId, string VersionId);

public record DeleteSmartFormCommand(string Id);
public record GetSmartFormsQuery;
public record GetSmartFormByIdQuery(string Id);
public record GenerateFormFromSmartObjectCommand(string SmartObjectId, string? LinkedWorkflowKey = null);

public record SubmitSmartFormCommand(
    string SmartFormId,
    string SubmittedBy,
    Dictionary<string, object?> Data,
    string? IdempotencyKey = null,
    bool StartLinkedWorkflow = true,
    string? DraftId = null
);

public record SaveSmartFormDraftCommand(
    string SmartFormId,
    string SubmittedBy,
    Dictionary<string, object?> Data,
    string? DraftId = null
);

public record SaveSmartFormDraftResult(
    SmartFormSubmissionDto Submission,
    bool Created
);

public record GetMySmartFormSubmissionsQuery(
    string SubmittedBy,
    string? AlternateUserId = null
);

public record GetSmartFormSubmissionsQuery(string SmartFormId);

public record UploadAttachmentCommand(
    string FileName,
    string ContentType,
    byte[] Content,
    string UploadedByUserId,
    string? WorkflowInstanceId = null,
    string? SubmissionId = null
);

public static class SmartFormQuotas
{
    public const int MaxFieldsCount = 200;
    public const int MaxFieldsJsonBytes = 512 * 1024;      // 512 KB
    public const int MaxSubmissionDataBytes = 256 * 1024;  // 256 KB
    public const int MaxIdempotencyKeyLength = 128;
}

public class CreateSmartFormValidator : AbstractValidator<CreateSmartFormCommand>
{
    public CreateSmartFormValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.Title)
            .MaximumLength(255);

        RuleFor(x => x.Fields)
            .Must(f => f == null || f.Count <= SmartFormQuotas.MaxFieldsCount)
            .WithMessage($"A SmartForm cannot exceed {SmartFormQuotas.MaxFieldsCount} fields.");

        RuleFor(x => x.Fields)
            .Must(f => f == null || Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(f)) <= SmartFormQuotas.MaxFieldsJsonBytes)
            .WithMessage($"SmartForm FieldsJson cannot exceed {SmartFormQuotas.MaxFieldsJsonBytes / 1024} KB.");
    }
}

public class UpdateSmartFormValidator : AbstractValidator<UpdateSmartFormCommand>
{
    public UpdateSmartFormValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(255);

        RuleFor(x => x.Fields)
            .Must(f => f == null || f.Count <= SmartFormQuotas.MaxFieldsCount)
            .WithMessage($"A SmartForm cannot exceed {SmartFormQuotas.MaxFieldsCount} fields.");

        RuleFor(x => x.Fields)
            .Must(f => f == null || Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(f)) <= SmartFormQuotas.MaxFieldsJsonBytes)
            .WithMessage($"SmartForm FieldsJson cannot exceed {SmartFormQuotas.MaxFieldsJsonBytes / 1024} KB.");
    }
}

/// <summary>
/// Wolverine CQRS handler for SmartForms CRUD, 1-click SmartObject generation,
/// Zero-Trust form submissions, and secure file attachments.
/// </summary>
public class SmartFormHandler
{
    private readonly ISmartFormRepository _formRepository;
    private readonly ISmartObjectRepository _smartObjectRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly ISmartFormValidationEngine _validationEngine;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkflowInstanceStarter _workflowStarter;
    private readonly IWorkflowInstanceRepository? _instanceRepository;

    public SmartFormHandler(
        ISmartFormRepository formRepository,
        ISmartObjectRepository smartObjectRepository,
        IBlobStorageService blobStorage,
        ISmartFormValidationEngine validationEngine,
        IUnitOfWork unitOfWork,
        IWorkflowInstanceStarter workflowStarter,
        IWorkflowInstanceRepository? instanceRepository = null)
    {
        _formRepository = formRepository ?? throw new ArgumentNullException(nameof(formRepository));
        _smartObjectRepository = smartObjectRepository ?? throw new ArgumentNullException(nameof(smartObjectRepository));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _validationEngine = validationEngine ?? throw new ArgumentNullException(nameof(validationEngine));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _workflowStarter = workflowStarter ?? throw new ArgumentNullException(nameof(workflowStarter));
        _instanceRepository = instanceRepository;
    }

    public async Task<IReadOnlyList<SmartFormDto>> Handle(GetSmartFormsQuery _, CancellationToken ct)
    {
        var entities = await _formRepository.GetAllAsync(ct);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<SmartFormDto?> Handle(GetSmartFormByIdQuery query, CancellationToken ct)
    {
        var entity = await _formRepository.GetByIdAsync(query.Id, ct);
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<SmartFormDto> Handle(CreateSmartFormCommand command, CancellationToken ct)
    {
        string id = !string.IsNullOrWhiteSpace(command.Id) ? command.Id : $"form-{Guid.NewGuid():N}"[..20];
        string title = !string.IsNullOrWhiteSpace(command.Title) ? command.Title : command.Name;
        var fields = command.Fields ?? new List<SmartFormFieldDto>();
        var rules = command.Rules ?? new List<FormRuleDto>();

        // Reject circular dependencies in the rule DAG before persisting
        FormRuleDependencyGraph.EnsureAcyclic(fields, rules);

        string fieldsJson = JsonSerializer.Serialize(fields);
        string rulesJson = JsonSerializer.Serialize(rules);

        var existing = await _formRepository.GetByIdAsync(id, ct);
        if (existing != null)
        {
            EnsureExpectedVersion(existing, command.ExpectedVersion);

            existing.UpdateDraft(
                title,
                command.Description ?? string.Empty,
                command.Category ?? "General",
                fieldsJson,
                command.LinkedSmartObjectId,
                command.LinkedWorkflowKey,
                rulesJson);

            if (command.PublishImmediately)
            {
                existing.PublishDraft(command.PublishedBy ?? "system");
            }

            await _formRepository.UpdateAsync(existing, ct);
            return MapToDto(existing);
        }

        var entity = new SmartFormEntity(
            id,
            command.Name,
            title,
            command.Description ?? string.Empty,
            command.Category ?? "General",
            fieldsJson,
            command.LinkedSmartObjectId,
            command.LinkedWorkflowKey,
            status: command.PublishImmediately ? "Published" : "Draft",
            publishImmediately: command.PublishImmediately,
            publishedBy: command.PublishedBy,
            rulesJson: rulesJson);

        await _formRepository.AddAsync(entity, ct);
        return MapToDto(entity);
    }

    public async Task<SmartFormDto?> Handle(UpdateSmartFormCommand command, CancellationToken ct)
    {
        var existing = await _formRepository.GetByIdAsync(command.Id, ct);
        if (existing == null) return null;

        EnsureExpectedVersion(existing, command.ExpectedVersion);

        var baseDraft = existing.GetCurrentDraftVersion();
        var effectiveFields = command.Fields
            ?? DeserializeSmartFormFields(baseDraft?.FieldsJson ?? existing.FieldsJson);
        var effectiveRules = command.Rules
            ?? DeserializeFormRules(baseDraft?.RulesJson ?? existing.RulesJson);

        // Reject circular dependencies in the rule DAG before persisting
        FormRuleDependencyGraph.EnsureAcyclic(effectiveFields, effectiveRules);

        string? fieldsJson = command.Fields != null
            ? JsonSerializer.Serialize(command.Fields)
            : null;
        string? rulesJson = command.Rules != null
            ? JsonSerializer.Serialize(command.Rules)
            : null;

        existing.UpdateDraft(
            command.Title ?? existing.Title,
            command.Description ?? existing.Description,
            command.Category ?? existing.Category,
            fieldsJson,
            command.LinkedSmartObjectId ?? existing.LinkedSmartObjectId,
            command.LinkedWorkflowKey ?? existing.LinkedWorkflowKey,
            rulesJson);

        await _formRepository.UpdateAsync(existing, ct);
        return MapToDto(existing);
    }

    public async Task<SmartFormDto?> Handle(PublishSmartFormCommand command, CancellationToken ct)
    {
        var existing = await _formRepository.GetByIdAsync(command.Id, ct);
        if (existing == null) return null;

        var draft = existing.GetCurrentDraftVersion();
        if (draft != null)
        {
            FormRuleDependencyGraph.EnsureAcyclic(
                DeserializeSmartFormFields(draft.FieldsJson),
                DeserializeFormRules(draft.RulesJson));
        }

        existing.PublishDraft(command.PublishedBy);
        await _formRepository.UpdateAsync(existing, ct);
        return MapToDto(existing);
    }

    public async Task<IReadOnlyList<SmartFormVersionSummaryDto>?> Handle(GetSmartFormVersionsQuery query, CancellationToken ct)
    {
        var form = await _formRepository.GetByIdAsync(query.SmartFormId, ct);
        if (form == null) return null;

        var versions = await _formRepository.GetVersionsAsync(query.SmartFormId, ct);
        return versions.Select(v => new SmartFormVersionSummaryDto(
            v.Id,
            v.SmartFormId,
            v.VersionNumber,
            v.Status.ToString(),
            v.PublishedAt,
            v.PublishedBy,
            v.CreatedAt)).ToList();
    }

    public async Task<SmartFormVersionDetailDto?> Handle(GetSmartFormVersionByIdQuery query, CancellationToken ct)
    {
        var version = await _formRepository.GetVersionByIdAsync(query.SmartFormId, query.VersionId, ct);
        if (version == null) return null;

        return new SmartFormVersionDetailDto(
            version.Id,
            version.SmartFormId,
            version.VersionNumber,
            version.Status.ToString(),
            DeserializeSmartFormFields(version.FieldsJson),
            version.PublishedAt,
            version.PublishedBy,
            version.CreatedAt,
            DeserializeFormRules(version.RulesJson));
    }

    public async Task<bool> Handle(DeleteSmartFormCommand command, CancellationToken ct)
    {
        return await _formRepository.DeleteAsync(command.Id, ct);
    }

    public async Task<SmartFormDto> Handle(GenerateFormFromSmartObjectCommand command, CancellationToken ct)
    {
        var smartObject = await _smartObjectRepository.GetByIdAsync(command.SmartObjectId, ct);
        if (smartObject == null)
        {
            throw new KeyNotFoundException($"SmartObject '{command.SmartObjectId}' not found.");
        }

        var soFields = DeserializeSmartObjectFields(smartObject.FieldsJson);
        var generatedFields = new List<SmartFormFieldDto>();

        int idx = 1;
        foreach (var sf in soFields)
        {
            string mappedType = sf.Type.ToLowerInvariant() switch
            {
                "number" => sf.Name.Contains("amount", StringComparison.OrdinalIgnoreCase) ||
                            sf.Name.Contains("salary", StringComparison.OrdinalIgnoreCase) ||
                            sf.Name.Contains("price", StringComparison.OrdinalIgnoreCase)
                    ? "currency"
                    : "number",
                "boolean" => "checkbox",
                "datetime" => "date",
                _ => "text"
            };

            bool isPk = sf.IsPrimaryKey ?? false;
            bool isReq = sf.IsRequired ?? false;

            generatedFields.Add(new SmartFormFieldDto(
                Id: $"fld-{idx++}",
                Name: sf.Name,
                Label: FormatHumanLabel(sf.Name),
                Type: mappedType,
                Required: isReq && !isPk,
                ReadOnly: isPk,
                Width: "half",
                Placeholder: isPk ? "Auto-generated ID" : $"Enter {FormatHumanLabel(sf.Name)}..."
            ));
        }

        string formId = $"form-{smartObject.Id}";
        var formEntity = new SmartFormEntity(
            id: formId,
            name: $"{smartObject.Name}Form",
            title: $"{smartObject.DisplayName} Form",
            description: $"Auto-generated SmartForm bound to {smartObject.DisplayName} ({smartObject.Name}).",
            category: smartObject.Category,
            fieldsJson: JsonSerializer.Serialize(generatedFields),
            linkedSmartObjectId: smartObject.Id,
            linkedWorkflowKey: command.LinkedWorkflowKey,
            status: "Published",
            publishImmediately: true,
            publishedBy: "system"
        );

        var existing = await _formRepository.GetByIdAsync(formId, ct);
        if (existing != null)
        {
            existing.UpdateDraft(
                formEntity.Title,
                formEntity.Description,
                formEntity.Category,
                formEntity.FieldsJson,
                formEntity.LinkedSmartObjectId,
                formEntity.LinkedWorkflowKey);
            existing.PublishDraft("system");
            await _formRepository.UpdateAsync(existing, ct);
            return MapToDto(existing);
        }

        await _formRepository.AddAsync(formEntity, ct);
        return MapToDto(formEntity);
    }

    public async Task<SmartFormSubmissionDto> Handle(SubmitSmartFormCommand command, CancellationToken ct)
    {
        string rawPayloadJson = JsonSerializer.Serialize(command.Data ?? new Dictionary<string, object?>());
        if (Encoding.UTF8.GetByteCount(rawPayloadJson) > SmartFormQuotas.MaxSubmissionDataBytes)
        {
            throw new SmartFormValidationFailedException(
                SmartFormValidationFailedException.PayloadTooLargeCode,
                $"{SmartFormValidationFailedException.PayloadTooLargeCode}: Form submission payload exceeds the maximum allowed size of {SmartFormQuotas.MaxSubmissionDataBytes / 1024} KB.");
        }

        string? idempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey) ? null : command.IdempotencyKey.Trim();
        if (idempotencyKey != null && idempotencyKey.Length > SmartFormQuotas.MaxIdempotencyKeyLength)
        {
            throw new SmartFormValidationFailedException(
                "INVALID_IDEMPOTENCY_KEY",
                $"Idempotency-Key cannot exceed {SmartFormQuotas.MaxIdempotencyKeyLength} characters.");
        }

        // Single atomic unit: submission + SmartObject record + workflow instance + attachment binding.
        // Any failure rolls the whole transaction back and surfaces as an explicit error (never a silent success).
        return await _unitOfWork.ExecuteInTransactionAsync(
            token => SubmitInTransactionAsync(command, idempotencyKey, token),
            ct);
    }

    private async Task<SmartFormSubmissionDto> SubmitInTransactionAsync(
        SubmitSmartFormCommand command,
        string? idempotencyKey,
        CancellationToken ct)
    {
        var form = await _formRepository.GetByIdAsync(command.SmartFormId, ct);
        if (form == null)
        {
            throw new KeyNotFoundException($"SmartForm '{command.SmartFormId}' not found.");
        }

        // 1. Idempotent replay: return the already-recorded result without side effects
        if (idempotencyKey != null)
        {
            var previous = await _formRepository.GetSubmissionByIdempotencyKeyAsync(form.Id, idempotencyKey, ct);
            if (previous != null)
            {
                if (!string.Equals(previous.SubmittedBy, command.SubmittedBy, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IdempotencyKeyConflictException(
                        "This Idempotency-Key was already used by a different caller.");
                }

                return MapSubmission(previous, replay: true);
            }
        }

        // 2. Resolve the active immutable Published version (rejects with 422 if only Draft exists)
        var activeVersion = form.GetActivePublishedVersion();
        if (activeVersion == null)
        {
            throw new SmartFormNotPublishedException(
                $"{SmartFormNotPublishedException.ErrorCode}: SmartForm '{form.Id}' has no Published version and cannot accept submissions.");
        }

        // 3. Zero-Trust server validation against the active Published version schema & JSONLogic rules
        var validation = _validationEngine.ValidateSubmission(activeVersion.FieldsJson, command.Data, activeVersion.RulesJson);
        if (!validation.IsValid)
        {
            throw new SmartFormValidationFailedException(
                validation.ErrorCode ?? SmartFormValidationFailedException.ValidationFailedCode,
                "SmartForm submission failed server-side schema and rule validation.",
                validation.Errors);
        }

        var sanitizedData = validation.SanitizedData;
        string dataJson = JsonSerializer.Serialize(sanitizedData);

        SmartFormSubmissionEntity? existingDraft = null;
        if (!string.IsNullOrWhiteSpace(command.DraftId))
        {
            existingDraft = await _formRepository.GetSubmissionByIdAsync(command.DraftId.Trim(), ct);
            if (existingDraft == null || !string.Equals(existingDraft.SmartFormId, form.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new KeyNotFoundException($"Draft submission '{command.DraftId}' was not found for SmartForm '{form.Id}'.");
            }

            if (!string.Equals(existingDraft.SubmittedBy, command.SubmittedBy, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"User '{command.SubmittedBy}' is not allowed to submit draft '{existingDraft.Id}'.");
            }

            if (!existingDraft.IsDraft)
            {
                throw new InvalidOperationException($"Submission '{existingDraft.Id}' has already been submitted.");
            }
        }

        // 4. Persist canonical SmartObject record first (if bound) so SmartFormSubmission links directly to SmartObjectRecordId
        long? smartObjectRecordId = null;
        if (!string.IsNullOrWhiteSpace(form.LinkedSmartObjectId) && sanitizedData.Count > 0)
        {
            var so = await _smartObjectRepository.GetByIdAsync(form.LinkedSmartObjectId, ct);
            if (so != null)
            {
                string recordKey = $"REC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
                var recordEntity = new SmartObjectRecordEntity(so.Id, recordKey, dataJson);
                // AddRecordAsync saves and populates recordEntity.Id while maintaining the SmartObject records counter
                var savedRecord = await _smartObjectRepository.AddRecordAsync(recordEntity, ct);
                smartObjectRecordId = savedRecord.Id;
            }
        }

        SmartFormSubmissionEntity submission;
        if (existingDraft != null)
        {
            existingDraft.CompleteDraft(dataJson, activeVersion.Id, smartObjectRecordId, idempotencyKey);
            await _formRepository.UpdateSubmissionAsync(existingDraft, ct);
            submission = existingDraft;
        }
        else
        {
            submission = new SmartFormSubmissionEntity(
                $"sub-{Guid.NewGuid():N}"[..20],
                form.Id,
                command.SubmittedBy,
                dataJson,
                linkedInstanceId: null,
                idempotencyKey: idempotencyKey,
                smartFormVersionId: activeVersion.Id,
                smartObjectRecordId: smartObjectRecordId,
                status: SmartFormSubmissionEntity.StatusSubmitted);

            await _formRepository.AddSubmissionAsync(submission, ct);
        }

        form.IncrementSubmissions();
        await _formRepository.UpdateAsync(form, ct);

        // 4. Start the linked workflow in the same transaction; failures propagate and roll everything back
        if (command.StartLinkedWorkflow && !string.IsNullOrWhiteSpace(form.LinkedWorkflowKey))
        {
            var triggerVariables = sanitizedData
                .Where(kv => kv.Value is not null)
                .ToDictionary(kv => kv.Key, kv => kv.Value!);

            WorkflowEngine.Domain.Entities.WorkflowInstance instance;
            try
            {
                instance = await _workflowStarter.StartAsync(
                    form.LinkedWorkflowKey,
                    command.SubmittedBy,
                    triggerVariables,
                    ct);
            }
            catch (InvalidOperationException ex)
            {
                throw new WorkflowStartFailedException(
                    $"{WorkflowStartFailedException.ErrorCode}: Linked workflow '{form.LinkedWorkflowKey}' could not be started. {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new WorkflowStartFailedException(
                    $"{WorkflowStartFailedException.ErrorCode}: Linked workflow '{form.LinkedWorkflowKey}' has an invalid definition.", ex);
            }

            if (instance.Status == WorkflowEngine.Domain.Enums.WorkflowStatus.Failed)
            {
                string reason = instance.Errors.LastOrDefault()?.ErrorMessage ?? "trigger execution failed";
                throw new WorkflowStartFailedException(
                    $"{WorkflowStartFailedException.ErrorCode}: Linked workflow '{form.LinkedWorkflowKey}' failed during start: {reason}");
            }

            instance.PinSmartFormVersion(activeVersion.Id);
            submission.LinkWorkflowInstance(instance.InstanceId);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        // 5. Bind attachments owned by the submitter to this submission / instance
        foreach (var attachmentId in ExtractAttachmentIds(sanitizedData))
        {
            var att = await _formRepository.GetAttachmentByIdAsync(attachmentId, ct);
            if (att != null && string.Equals(att.UploadedByUserId, command.SubmittedBy, StringComparison.OrdinalIgnoreCase))
            {
                att.BindToWorkflowSubmission(submission.Id, submission.LinkedInstanceId);
                await _formRepository.UpdateAttachmentAsync(att, ct);
            }
        }

        return new SmartFormSubmissionDto(
            submission.Id,
            submission.SmartFormId,
            submission.SubmittedBy,
            sanitizedData,
            submission.LinkedInstanceId,
            submission.SubmittedAt,
            IdempotentReplay: false,
            SmartFormVersionId: submission.SmartFormVersionId,
            SmartObjectRecordId: submission.SmartObjectRecordId,
            Status: submission.Status,
            UpdatedAt: submission.UpdatedAt,
            SmartFormTitle: form.Title,
            SmartFormCategory: form.Category);
    }

    public async Task<SaveSmartFormDraftResult> Handle(SaveSmartFormDraftCommand command, CancellationToken ct)
    {
        var payload = command.Data ?? new Dictionary<string, object?>();
        string dataJson = JsonSerializer.Serialize(payload);
        if (Encoding.UTF8.GetByteCount(dataJson) > SmartFormQuotas.MaxSubmissionDataBytes)
        {
            throw new SmartFormValidationFailedException(
                SmartFormValidationFailedException.PayloadTooLargeCode,
                $"{SmartFormValidationFailedException.PayloadTooLargeCode}: Draft payload exceeds the maximum allowed size of {SmartFormQuotas.MaxSubmissionDataBytes / 1024} KB.");
        }

        var form = await _formRepository.GetByIdAsync(command.SmartFormId, ct);
        if (form == null)
        {
            throw new KeyNotFoundException($"SmartForm '{command.SmartFormId}' not found.");
        }

        var activeVersion = form.GetActivePublishedVersion() ?? form.GetLatestVersion();

        if (!string.IsNullOrWhiteSpace(command.DraftId))
        {
            var existing = await _formRepository.GetSubmissionByIdAsync(command.DraftId.Trim(), ct);
            if (existing == null || !string.Equals(existing.SmartFormId, form.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new KeyNotFoundException($"Draft '{command.DraftId}' not found for SmartForm '{form.Id}'.");
            }

            if (!string.Equals(existing.SubmittedBy, command.SubmittedBy, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"User '{command.SubmittedBy}' is not authorized to modify draft '{existing.Id}'.");
            }

            if (!existing.IsDraft)
            {
                throw new InvalidOperationException($"Submission '{existing.Id}' is already submitted and cannot be modified as a draft.");
            }

            existing.UpdateDraftData(dataJson, activeVersion?.Id);
            await _formRepository.UpdateSubmissionAsync(existing, ct);
            return new SaveSmartFormDraftResult(MapSubmission(existing, replay: false, form.Title, form.Category), Created: false);
        }

        var draft = new SmartFormSubmissionEntity(
            $"drf-{Guid.NewGuid():N}"[..20],
            form.Id,
            command.SubmittedBy,
            dataJson,
            linkedInstanceId: null,
            idempotencyKey: null,
            smartFormVersionId: activeVersion?.Id,
            smartObjectRecordId: null,
            status: SmartFormSubmissionEntity.StatusDraft);

        await _formRepository.AddSubmissionAsync(draft, ct);
        return new SaveSmartFormDraftResult(MapSubmission(draft, replay: false, form.Title, form.Category), Created: true);
    }

    public async Task<IReadOnlyList<SmartFormSubmissionDto>> Handle(GetMySmartFormSubmissionsQuery query, CancellationToken ct)
    {
        var submissions = await _formRepository.GetSubmissionsByUserAsync(query.SubmittedBy, query.AlternateUserId, ct);
        if (submissions.Count == 0)
        {
            return Array.Empty<SmartFormSubmissionDto>();
        }

        var forms = await _formRepository.GetAllAsync(ct);
        var formLookup = forms.ToDictionary(f => f.Id, StringComparer.OrdinalIgnoreCase);

        var instanceLookup = new Dictionary<string, (string Status, string? CurrentStep)>(StringComparer.OrdinalIgnoreCase);
        if (_instanceRepository != null)
        {
            var linkedIds = submissions
                .Where(s => !string.IsNullOrWhiteSpace(s.LinkedInstanceId))
                .Select(s => s.LinkedInstanceId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var instId in linkedIds)
            {
                var inst = await _instanceRepository.GetByIdAsync(instId, ct);
                if (inst != null)
                {
                    instanceLookup[instId] = (inst.Status.ToString(), inst.CurrentNodeId);
                }
            }
        }

        return submissions.Select(s =>
        {
            formLookup.TryGetValue(s.SmartFormId, out var formMeta);
            string? instStatus = null;
            string? currentStep = null;
            if (!string.IsNullOrWhiteSpace(s.LinkedInstanceId) &&
                instanceLookup.TryGetValue(s.LinkedInstanceId, out var instInfo))
            {
                instStatus = instInfo.Status;
                currentStep = instInfo.CurrentStep;
            }

            return MapSubmission(
                s,
                replay: false,
                formTitle: formMeta?.Title,
                formCategory: formMeta?.Category,
                instanceStatus: instStatus,
                currentStep: currentStep);
        }).ToList();
    }

    private static SmartFormSubmissionDto MapSubmission(
        SmartFormSubmissionEntity s,
        bool replay,
        string? formTitle = null,
        string? formCategory = null,
        string? instanceStatus = null,
        string? currentStep = null)
    {
        Dictionary<string, object?> parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(s.DataJson) ?? new();
        }
        catch
        {
            parsed = new();
        }

        return new SmartFormSubmissionDto(
            s.Id,
            s.SmartFormId,
            s.SubmittedBy,
            parsed,
            s.LinkedInstanceId,
            s.SubmittedAt,
            replay,
            s.SmartFormVersionId,
            s.SmartObjectRecordId,
            s.Status,
            s.UpdatedAt,
            formTitle,
            formCategory,
            instanceStatus,
            currentStep);
    }

    private static void EnsureExpectedVersion(SmartFormEntity entity, int? expectedVersion)
    {
        if (expectedVersion.HasValue && expectedVersion.Value != entity.Version)
        {
            throw new ConcurrencyConflictException(
                $"{ConcurrencyConflictException.ErrorCode}: SmartForm '{entity.Id}' was modified by another user (expected version {expectedVersion.Value}, current version {entity.Version}). Reload and retry.");
        }
    }

    public async Task<IReadOnlyList<SmartFormSubmissionDto>> Handle(GetSmartFormSubmissionsQuery query, CancellationToken ct)
    {
        var list = await _formRepository.GetSubmissionsAsync(query.SmartFormId, ct);
        return list.Select(s =>
        {
            Dictionary<string, object?> parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(s.DataJson) ?? new();
            }
            catch
            {
                parsed = new();
            }

            return new SmartFormSubmissionDto(
                s.Id,
                s.SmartFormId,
                s.SubmittedBy,
                parsed,
                s.LinkedInstanceId,
                s.SubmittedAt,
                IdempotentReplay: false,
                SmartFormVersionId: s.SmartFormVersionId,
                SmartObjectRecordId: s.SmartObjectRecordId);
        }).ToList();
    }

    public async Task<FormAttachmentDto> Handle(UploadAttachmentCommand command, CancellationToken ct)
    {
        var validated = AttachmentSecurityValidator.ValidateAndInspect(
            command.FileName,
            command.ContentType,
            command.Content);

        var blob = await _blobStorage.SaveAsync(
            validated.SanitizedFileName,
            validated.CanonicalContentType,
            command.Content,
            ct);

        string id = $"att-{Guid.NewGuid():N}"[..20];

        var entity = new FormAttachmentEntity(
            id,
            validated.SanitizedFileName,
            validated.CanonicalContentType,
            blob.SizeBytes,
            blob.StoragePath,
            blob.Sha256Hash,
            command.UploadedByUserId,
            command.WorkflowInstanceId,
            command.SubmissionId);

        await _formRepository.AddAttachmentAsync(entity, ct);

        return new FormAttachmentDto(
            entity.Id,
            entity.FileName,
            entity.ContentType,
            entity.SizeBytes,
            entity.Sha256Hash,
            entity.UploadedByUserId,
            entity.UploadedAt,
            $"/api/attachments/{entity.Id}",
            entity.UploadedByUserId,
            entity.WorkflowInstanceId,
            entity.SubmissionId);
    }

    private static IEnumerable<string> ExtractAttachmentIds(Dictionary<string, object?> data)
    {
        foreach (var val in data.Values)
        {
            if (val is JsonElement el && el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                string? id = idProp.GetString();
                if (!string.IsNullOrWhiteSpace(id) && id.StartsWith("att-", StringComparison.OrdinalIgnoreCase))
                {
                    yield return id;
                }
            }
            else if (val is IDictionary<string, object?> dict &&
                     dict.TryGetValue("id", out var rawId) && rawId is string strId &&
                     strId.StartsWith("att-", StringComparison.OrdinalIgnoreCase))
            {
                yield return strId;
            }
        }
    }

    private static SmartFormDto MapToDto(SmartFormEntity e)
    {
        var fields = DeserializeSmartFormFields(e.FieldsJson);
        var rules = DeserializeFormRules(e.RulesJson);
        var draft = e.GetCurrentDraftVersion();
        var latest = e.GetLatestVersion();
        var draftFields = draft != null ? DeserializeSmartFormFields(draft.FieldsJson) : null;
        var draftRules = draft != null ? DeserializeFormRules(draft.RulesJson) : null;

        return new SmartFormDto(
            e.Id,
            e.Name,
            e.Title,
            e.Description,
            e.Category,
            e.LinkedSmartObjectId,
            e.LinkedWorkflowKey,
            e.Status,
            e.SubmissionsCount,
            fields,
            e.UpdatedAt,
            e.Version,
            e.ActivePublishedVersionId,
            latest?.VersionNumber ?? e.LatestVersionNumber,
            latest?.Id,
            latest?.Status.ToString() ?? e.Status,
            draft != null,
            draftFields,
            rules,
            draftRules);
    }

    private static List<FormRuleDto> DeserializeFormRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<FormRuleDto>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static List<SmartFormFieldDto> DeserializeSmartFormFields(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<SmartFormFieldDto>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static List<SmartObjectFieldDto> DeserializeSmartObjectFields(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<SmartObjectFieldDto>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static string FormatHumanLabel(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return "Field";
        var chars = new List<char> { char.ToUpperInvariant(identifier[0]) };
        for (int i = 1; i < identifier.Length; i++)
        {
            char c = identifier[i];
            if (c == '_')
            {
                chars.Add(' ');
                continue;
            }
            if (char.IsUpper(c) && !char.IsUpper(identifier[i - 1]) && identifier[i - 1] != '_')
            {
                chars.Add(' ');
            }
            chars.Add(c);
        }
        return new string(chars.ToArray());
    }
}
