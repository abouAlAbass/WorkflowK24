using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditService"/> that persists typed <see cref="SecurityAuditEvent"/>
/// records and enforces a strict guardrail stripping any key containing forbidden secret keywords.
/// </summary>
public sealed class SecurityAuditService : IAuditService
{
    public static readonly IReadOnlyList<string> ForbiddenMetadataKeywords = new[]
    {
        "password",
        "secret",
        "token",
        "connectionString",
        "authorization"
    };

    private readonly WorkflowEngineDbContext _context;

    public SecurityAuditService(WorkflowEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<SecurityAuditEvent> RecordAsync(
        SecurityAuditEventType eventType,
        string actorId,
        string targetType,
        string targetId,
        IDictionary<string, object?>? metadata = null,
        CancellationToken ct = default)
    {
        string? rawJson = metadata is null || metadata.Count == 0
            ? null
            : JsonSerializer.Serialize(metadata);

        return RecordAsync(eventType, actorId, targetType, targetId, rawJson, ct);
    }

    public async Task<SecurityAuditEvent> RecordAsync(
        SecurityAuditEventType eventType,
        string actorId,
        string targetType,
        string targetId,
        string? metadataJson,
        CancellationToken ct = default)
    {
        string? sanitizedMetadata = SanitizeMetadataJson(metadataJson);

        var auditEvent = new SecurityAuditEvent(
            eventType,
            actorId,
            targetType,
            targetId,
            sanitizedMetadata);

        await _context.SecurityAuditEvents.AddAsync(auditEvent, ct);
        await _context.SaveChangesAsync(ct);

        return auditEvent;
    }

    public async Task<IReadOnlyList<SecurityAuditEvent>> GetRecentAsync(
        int limit = 100,
        SecurityAuditEventType? eventType = null,
        CancellationToken ct = default)
    {
        IQueryable<SecurityAuditEvent> query = _context.SecurityAuditEvents.AsNoTracking();

        if (eventType.HasValue)
        {
            query = query.Where(e => e.EventType == eventType.Value);
        }

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Recursively removes all properties whose key contains any forbidden secret keyword.
    /// Rejects non-JSON payloads so raw secret strings cannot bypass sanitization.
    /// </summary>
    public static string? SanitizeMetadataJson(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(metadataJson);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Audit metadata must be valid JSON.", nameof(metadataJson), ex);
        }

        if (root is not JsonObject rootObj)
        {
            throw new ArgumentException("Audit metadata must be a JSON object.", nameof(metadataJson));
        }

        StripForbiddenKeys(rootObj);
        return rootObj.ToJsonString();
    }

    public static bool IsForbiddenKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        foreach (var keyword in ForbiddenMetadataKeywords)
        {
            if (key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void StripForbiddenKeys(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(kvp => kvp.Key).ToList();
            foreach (var key in keys)
            {
                if (IsForbiddenKey(key))
                {
                    obj.Remove(key);
                }
                else if (obj[key] is JsonNode child)
                {
                    StripForbiddenKeys(child);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null)
                {
                    StripForbiddenKeys(item);
                }
            }
        }
    }
}
