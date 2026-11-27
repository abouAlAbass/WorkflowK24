using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Common.Security;
using WorkflowEngine.Application.Features.Instances.Commands;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.SmartForms.DTOs;
using WorkflowEngine.Application.Features.SmartForms.Handlers;
using WorkflowEngine.Application.Features.SmartForms.Validation;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Api.Endpoints;

public record SubmitSmartFormRequest(
    Dictionary<string, object?> Data,
    bool StartLinkedWorkflow = true,
    string? DraftId = null
);

public record SaveSmartFormDraftRequest(
    Dictionary<string, object?> Data,
    string? DraftId = null
);

public record Base64AttachmentUploadRequest(
    string FileName,
    string ContentType,
    string Base64Content,
    string? WorkflowInstanceId = null,
    string? SubmissionId = null
);

public static class SmartFormEndpoints
{
    private static readonly HashSet<string> AdminRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Administrator",
        "BPM Administrator"
    };

    public static IEndpointRouteBuilder MapSmartFormEndpoints(this IEndpointRouteBuilder app)
    {
        var formsGroup = app.MapGroup("/api/smartforms")
            .WithTags("SmartForms")
            .RequireAuthorization();

        formsGroup.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyList<SmartFormDto>>(new GetSmartFormsQuery(), ct);
            return Results.Ok(result);
        })
        .WithName("GetSmartForms")
        .WithSummary("Retrieves all registered No-Code SmartForm definitions.");

        formsGroup.MapGet("/my-submissions", async (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            if (string.IsNullOrWhiteSpace(caller.CurrentUserId))
            {
                return Results.Unauthorized();
            }

            var result = await bus.InvokeAsync<IReadOnlyList<SmartFormSubmissionDto>>(
                new GetMySmartFormSubmissionsQuery(caller.CurrentUserId, caller.Sub), ct);
            return Results.Ok(result);
        })
        .WithName("GetMySmartFormSubmissions")
        .WithSummary("Retrieves the authenticated user's drafts and submitted requests with live workflow status.");

        formsGroup.MapGet("/{id}", async (string id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<SmartFormDto?>(new GetSmartFormByIdQuery(id), ct);
            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetSmartFormById")
        .WithSummary("Retrieves a single SmartForm schema by ID.");

        formsGroup.MapPost("/", async (
            CreateSmartFormCommand command,
            ClaimsPrincipal principal,
            IValidator<CreateSmartFormCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var caller = ExtractCallerIdentity(principal);
            string publishedBy = !string.IsNullOrWhiteSpace(caller.CurrentUserId) ? caller.CurrentUserId : "system";

            try
            {
                var created = await bus.InvokeAsync<SmartFormDto>(command with { PublishedBy = publishedBy }, ct);
                return Results.Created($"/api/smartforms/{created.Id}", created);
            }
            catch (CyclicFormRuleException ex)
            {
                return Results.Json(
                    new { code = CyclicFormRuleException.ErrorCode, error = ex.Message, cycle = ex.CyclePath },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            catch (SmartFormVersionImmutableException ex)
            {
                return Results.Json(
                    new { code = SmartFormVersionImmutableException.ErrorCode, error = ex.Message },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (ConcurrencyConflictException ex)
            {
                return ConcurrencyConflict(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyConflict("The SmartForm was modified by another user. Reload and retry.");
            }
        })
        .RequireAuthorization("designs.write")
        .WithName("CreateSmartForm")
        .WithSummary("Creates a SmartForm schema with initial version 1 (Draft by default, or Published when publishImmediately=true).");

        formsGroup.MapPut("/{id}", async (
            string id,
            UpdateSmartFormCommand command,
            IValidator<UpdateSmartFormCommand> validator,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command with { Id = id }, ct);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            try
            {
                var updated = await bus.InvokeAsync<SmartFormDto?>(command with { Id = id }, ct);
                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (CyclicFormRuleException ex)
            {
                return Results.Json(
                    new { code = CyclicFormRuleException.ErrorCode, error = ex.Message, cycle = ex.CyclePath },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            catch (SmartFormVersionImmutableException ex)
            {
                return Results.Json(
                    new { code = SmartFormVersionImmutableException.ErrorCode, error = ex.Message },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (ConcurrencyConflictException ex)
            {
                return ConcurrencyConflict(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyConflict($"SmartForm '{id}' was modified by another user. Reload and retry.");
            }
        })
        .RequireAuthorization("designs.write")
        .WithName("UpdateSmartForm")
        .WithSummary("Updates the current Draft version (or creates a new Draft version if the latest version is Published).");

        formsGroup.MapPost("/{id}/publish", async (
            string id,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            string publishedBy = !string.IsNullOrWhiteSpace(caller.CurrentUserId) ? caller.CurrentUserId : "system";

            try
            {
                var published = await bus.InvokeAsync<SmartFormDto?>(new PublishSmartFormCommand(id, publishedBy), ct);
                return published != null ? Results.Ok(published) : Results.NotFound();
            }
            catch (CyclicFormRuleException ex)
            {
                return Results.Json(
                    new { code = CyclicFormRuleException.ErrorCode, error = ex.Message, cycle = ex.CyclePath },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            catch (SmartFormVersionImmutableException ex)
            {
                return Results.Json(
                    new { code = SmartFormVersionImmutableException.ErrorCode, error = ex.Message },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(
                    new { code = "NO_DRAFT_TO_PUBLISH", error = ex.Message },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyConflict($"SmartForm '{id}' was modified concurrently during publish. Reload and retry.");
            }
        })
        .RequireAuthorization("designs.write")
        .WithName("PublishSmartForm")
        .WithSummary("Publishes the current Draft SmartFormVersion, archives any previous Published version, and activates the new version.");

        formsGroup.MapGet("/{id}/versions", async (string id, IMessageBus bus, CancellationToken ct) =>
        {
            var versions = await bus.InvokeAsync<IReadOnlyList<SmartFormVersionSummaryDto>?>(
                new GetSmartFormVersionsQuery(id), ct);
            return versions != null ? Results.Ok(versions) : Results.NotFound();
        })
        .WithName("GetSmartFormVersions")
        .WithSummary("Lists all versions (Draft, Published, Archived) of a SmartForm.");

        formsGroup.MapGet("/{id}/versions/{versionId}", async (string id, string versionId, IMessageBus bus, CancellationToken ct) =>
        {
            var version = await bus.InvokeAsync<SmartFormVersionDetailDto?>(
                new GetSmartFormVersionByIdQuery(id, versionId), ct);
            return version != null ? Results.Ok(version) : Results.NotFound();
        })
        .WithName("GetSmartFormVersionById")
        .WithSummary("Retrieves the immutable schema snapshot of a specific SmartFormVersion.");

        formsGroup.MapDelete("/{id}", async (string id, IMessageBus bus, CancellationToken ct) =>
        {
            var deleted = await bus.InvokeAsync<bool>(new DeleteSmartFormCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization("designs.write")
        .WithName("DeleteSmartForm")
        .WithSummary("Deletes a SmartForm definition and its submissions.");

        formsGroup.MapPost("/generate/{smartObjectId}", async (
            string smartObjectId,
            string? workflowKey,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            try
            {
                var generated = await bus.InvokeAsync<SmartFormDto>(
                    new GenerateFormFromSmartObjectCommand(smartObjectId, workflowKey), ct);
                return Results.Created($"/api/smartforms/{generated.Id}", generated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthorization("designs.write")
        .WithName("GenerateSmartFormFromObject")
        .WithSummary("Auto-generates a SmartForm schema from an existing SmartObject in one click.");

        formsGroup.MapPost("/{id}/drafts", async (
            string id,
            SaveSmartFormDraftRequest request,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var caller = ExtractCallerIdentity(principal);
            if (string.IsNullOrWhiteSpace(caller.CurrentUserId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await bus.InvokeAsync<SaveSmartFormDraftResult>(
                    new SaveSmartFormDraftCommand(
                        id,
                        caller.CurrentUserId,
                        request.Data ?? new Dictionary<string, object?>(),
                        request.DraftId),
                    ct);

                return result.Created
                    ? Results.Created($"/api/smartforms/{id}/drafts/{result.Submission.Id}", result.Submission)
                    : Results.Ok(result.Submission);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(
                    new { code = "FORBIDDEN", error = ex.Message },
                    statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(
                    new { code = "DRAFT_ALREADY_SUBMITTED", error = ex.Message },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (SmartFormValidationFailedException ex)
            {
                return Results.BadRequest(new { code = ex.ErrorCode, error = ex.Message });
            }
        })
        .WithName("SaveSmartFormDraft")
        .WithSummary("Saves or updates a draft SmartForm submission for the authenticated user without triggering the workflow.");

        formsGroup.MapPost("/{id}/submit", async (
            string id,
            SubmitSmartFormRequest request,
            HttpRequest httpRequest,
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            string rawPayloadJson = JsonSerializer.Serialize(request.Data ?? new Dictionary<string, object?>());
            if (Encoding.UTF8.GetByteCount(rawPayloadJson) > SmartFormQuotas.MaxSubmissionDataBytes)
            {
                return Results.BadRequest(new
                {
                    code = SmartFormValidationFailedException.PayloadTooLargeCode,
                    error = $"{SmartFormValidationFailedException.PayloadTooLargeCode}: Form submission payload exceeds the maximum allowed size of {SmartFormQuotas.MaxSubmissionDataBytes / 1024} KB."
                });
            }

            string? idempotencyKey = httpRequest.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                idempotencyKey = null;
            }
            else if (idempotencyKey.Length > SmartFormQuotas.MaxIdempotencyKeyLength)
            {
                return Results.BadRequest(new
                {
                    code = "INVALID_IDEMPOTENCY_KEY",
                    error = $"Idempotency-Key cannot exceed {SmartFormQuotas.MaxIdempotencyKeyLength} characters."
                });
            }

            var caller = ExtractCallerIdentity(principal);
            string userEmail = !string.IsNullOrWhiteSpace(caller.CurrentUserId)
                ? caller.CurrentUserId
                : "initiator@enterprise.internal";

            var command = new SubmitSmartFormCommand(
                id,
                userEmail,
                request.Data ?? new Dictionary<string, object?>(),
                idempotencyKey,
                request.StartLinkedWorkflow,
                request.DraftId);

            // Validation, idempotency check, persistence and workflow start all run inside ONE
            // transactional handler. Errors are mapped explicitly - nothing is swallowed.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    var submission = await bus.InvokeAsync<SmartFormSubmissionDto>(command, ct);

                    return submission.IdempotentReplay
                        ? Results.Ok(submission)
                        : Results.Created($"/api/smartforms/{id}/submissions/{submission.Id}", submission);
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
                catch (UnauthorizedAccessException ex)
                {
                    return Results.Json(
                        new { code = "FORBIDDEN", error = ex.Message },
                        statusCode: StatusCodes.Status403Forbidden);
                }
                catch (SmartFormNotPublishedException ex)
                {
                    return Results.Json(
                        new { code = SmartFormNotPublishedException.ErrorCode, error = ex.Message },
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                }
                catch (SmartFormValidationFailedException ex)
                {
                    return Results.ValidationProblem(
                        ex.Errors.ToDictionary(k => k.Key, v => v.Value),
                        detail: ex.Message,
                        extensions: new Dictionary<string, object?> { ["code"] = ex.ErrorCode });
                }
                catch (WorkflowStartFailedException ex)
                {
                    return Results.Json(
                        new { code = WorkflowStartFailedException.ErrorCode, error = ex.Message },
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                }
                catch (IdempotencyKeyConflictException ex)
                {
                    return Results.Json(
                        new { code = IdempotencyKeyConflictException.ErrorCode, error = ex.Message },
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Json(
                        new { code = "DRAFT_ALREADY_SUBMITTED", error = ex.Message },
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return ConcurrencyConflict("The SmartForm was modified concurrently. Retry the submission.");
                }
                catch (DbUpdateException) when (idempotencyKey != null && attempt == 0)
                {
                    // Lost a race on the unique (form, Idempotency-Key) index: the winner is committed,
                    // so one retry resolves to the idempotent replay of the stored result.
                }
            }
        })
        .WithName("SubmitSmartForm")
        .WithSummary("Submits form data, stores bound SmartObject records, and optionally starts the linked workflow.");

        formsGroup.MapGet("/{id}/submissions", async (string id, IMessageBus bus, CancellationToken ct) =>
        {
            var list = await bus.InvokeAsync<IReadOnlyList<SmartFormSubmissionDto>>(
                new GetSmartFormSubmissionsQuery(id), ct);
            return Results.Ok(list);
        })
        .WithName("GetSmartFormSubmissions")
        .WithSummary("Retrieves historical submissions for a SmartForm.");

        // Secure Blob Attachment Endpoints (Anti-IDOR, Magic-Byte Whitelist, Full EICAR Scan)
        var attachGroup = app.MapGroup("/api/attachments")
            .WithTags("Attachments")
            .RequireAuthorization();

        attachGroup.MapPost("/", HandleAttachmentUploadAsync)
            .WithName("UploadAttachmentRoot")
            .WithSummary("Validates magic bytes, scans for malware, hashes (SHA-256), and stores an attachment owned by the JWT caller.");

        attachGroup.MapPost("/upload", HandleAttachmentUploadAsync)
            .WithName("UploadAttachment")
            .WithSummary("Validates magic bytes, scans for malware, hashes (SHA-256), and stores an attachment owned by the JWT caller.");

        attachGroup.MapGet("/{id}", async (
            string id,
            HttpContext httpContext,
            ClaimsPrincipal principal,
            ISmartFormRepository formRepo,
            IWorkflowInstanceRepository instanceRepo,
            IBlobStorageService blobStorage,
            CancellationToken ct) =>
        {
            var meta = await formRepo.GetAttachmentByIdAsync(id, ct);
            if (meta == null) return Results.NotFound();

            var caller = ExtractCallerIdentity(principal);
            bool authorized = await IsAuthorizedToReadAttachmentAsync(meta, caller, instanceRepo, ct);
            if (!authorized)
            {
                return Results.Json(
                    new { code = "FORBIDDEN", error = $"User '{caller.CurrentUserId}' is not authorized to access attachment '{id}'." },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var bytes = await blobStorage.ReadAsync(meta.StoragePath, ct);
            if (bytes == null) return Results.NotFound();

            string safeFileName = AttachmentSecurityValidator.SanitizeDownloadFileName(meta.FileName);
            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            httpContext.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{safeFileName}\"";

            return Results.Bytes(bytes, contentType: meta.ContentType);
        })
        .WithName("DownloadAttachment")
        .WithSummary("Downloads a stored attachment with anti-IDOR authorization and anti-XSS attachment headers.");

        attachGroup.MapDelete("/{id}", async (
            string id,
            ClaimsPrincipal principal,
            ISmartFormRepository formRepo,
            IBlobStorageService blobStorage,
            CancellationToken ct) =>
        {
            var meta = await formRepo.GetAttachmentByIdAsync(id, ct);
            if (meta == null) return Results.NotFound();

            var caller = ExtractCallerIdentity(principal);
            bool canDelete = IsAdmin(caller.Roles) ||
                             MatchesActor(meta.UploadedByUserId, caller.CurrentUserId, caller.Sub);

            if (!canDelete)
            {
                return Results.Json(
                    new { code = "FORBIDDEN", error = $"User '{caller.CurrentUserId}' is not authorized to delete attachment '{id}'." },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            await blobStorage.DeleteAsync(meta.StoragePath, ct);
            await formRepo.DeleteAttachmentAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteAttachment")
        .WithSummary("Deletes a stored attachment (restricted to owner or Admin).");

        return app;
    }

    private static IResult ConcurrencyConflict(string message)
    {
        return Results.Json(
            new { code = ConcurrencyConflictException.ErrorCode, error = message },
            statusCode: StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleAttachmentUploadAsync(
        Base64AttachmentUploadRequest request,
        ClaimsPrincipal principal,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Base64Content))
        {
            return Results.BadRequest(new { code = "INVALID_PAYLOAD", error = "FileName and Base64Content are required." });
        }

        var caller = ExtractCallerIdentity(principal);
        if (string.IsNullOrWhiteSpace(caller.CurrentUserId))
        {
            return Results.Unauthorized();
        }

        byte[] rawBytes;
        try
        {
            string cleanBase64 = request.Base64Content.Contains(',')
                ? request.Base64Content[(request.Base64Content.IndexOf(',') + 1)..]
                : request.Base64Content;
            rawBytes = Convert.FromBase64String(cleanBase64);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { code = "INVALID_BASE64", error = "Invalid Base64 attachment payload." });
        }

        try
        {
            var dto = await bus.InvokeAsync<FormAttachmentDto>(
                new UploadAttachmentCommand(
                    request.FileName,
                    request.ContentType ?? "application/octet-stream",
                    rawBytes,
                    caller.CurrentUserId,
                    request.WorkflowInstanceId,
                    request.SubmissionId),
                ct);
            return Results.Created(dto.DownloadUrl, dto);
        }
        catch (AttachmentValidationException ex)
        {
            return Results.BadRequest(new { code = ex.ErrorCode, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { code = "UPLOAD_REJECTED", error = ex.Message });
        }
    }

    private static async Task<bool> IsAuthorizedToReadAttachmentAsync(
        FormAttachmentEntity meta,
        (string CurrentUserId, string? Sub, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups) caller,
        IWorkflowInstanceRepository instanceRepo,
        CancellationToken ct)
    {
        if (IsAdmin(caller.Roles) || MatchesActor(meta.UploadedByUserId, caller.CurrentUserId, caller.Sub))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(meta.WorkflowInstanceId))
        {
            return false;
        }

        var instance = await instanceRepo.GetByIdWithDetailsAsync(meta.WorkflowInstanceId, ct);
        if (instance == null)
        {
            return false;
        }

        if (MatchesActor(instance.InitiatorId, caller.CurrentUserId, caller.Sub))
        {
            return true;
        }

        foreach (var task in instance.Tasks)
        {
            if (task.AssigneeType == AssigneeType.User &&
                MatchesActor(task.AssigneeId, caller.CurrentUserId, caller.Sub))
            {
                return true;
            }

            if (task.AssigneeType == AssigneeType.Group &&
                (caller.Groups.Any(g => string.Equals(g, task.AssigneeId, StringComparison.OrdinalIgnoreCase)) ||
                 caller.Roles.Any(r => string.Equals(r, task.AssigneeId, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAdmin(IReadOnlyList<string> roles)
    {
        return roles.Any(r => AdminRoles.Contains(r.Trim()));
    }

    private static bool MatchesActor(string? expectedActor, string currentUserId, string? sub)
    {
        if (string.IsNullOrWhiteSpace(expectedActor))
        {
            return false;
        }

        return (!string.IsNullOrWhiteSpace(currentUserId) &&
                string.Equals(expectedActor, currentUserId, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(sub) &&
                string.Equals(expectedActor, sub, StringComparison.OrdinalIgnoreCase));
    }

    private static (string CurrentUserId, string? Sub, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups) ExtractCallerIdentity(ClaimsPrincipal principal)
    {
        string? email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);

        string? sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        string currentUserId = !string.IsNullOrWhiteSpace(email)
            ? email
            : (sub ?? principal.Identity?.Name ?? string.Empty);

        var roles = principal.FindAll(ClaimTypes.Role)
            .Concat(principal.FindAll("role"))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = principal.FindAll("group")
            .Concat(principal.FindAll(ClaimTypes.GroupSid))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (currentUserId, sub, roles, groups);
    }
}
