using System.Text.Json;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Features.Connections.Commands;
using WorkflowEngine.Application.Features.Connections.DTOs;
using WorkflowEngine.Application.Features.Connections.Queries;
using WorkflowEngine.Application.Services.Connections;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Connections.Handlers;

/// <summary>
/// Wolverine CQRS handlers for Connection CRUD, fine-grained ConnectionPermission governance,
/// connection diagnostics (/test), broker invocation (/invoke), and user personal credential vault (/me/connection-credentials).
/// </summary>
public class ConnectionHandlers
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IConnectionPermissionRepository _permissionRepository;
    private readonly IUserConnectionCredentialRepository _userCredentialRepository;
    private readonly IConnectionEnvironmentConfigRepository _environmentConfigRepository;
    private readonly IConnectionUsageLogRepository _usageLogRepository;
    private readonly IConnectionAccessService _accessService;
    private readonly ISecretProtector _secretProtector;
    private readonly IExecutionSecretResolver _secretResolver;
    private readonly IServiceBrokerRegistry _brokerRegistry;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConnectionHandlers> _logger;

    public ConnectionHandlers(
        IConnectionRepository connectionRepository,
        IConnectionPermissionRepository permissionRepository,
        IUserConnectionCredentialRepository userCredentialRepository,
        IConnectionEnvironmentConfigRepository environmentConfigRepository,
        IConnectionUsageLogRepository usageLogRepository,
        IConnectionAccessService accessService,
        ISecretProtector secretProtector,
        IExecutionSecretResolver secretResolver,
        IServiceBrokerRegistry brokerRegistry,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        ILogger<ConnectionHandlers> logger)
    {
        _connectionRepository = connectionRepository ?? throw new ArgumentNullException(nameof(connectionRepository));
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
        _userCredentialRepository = userCredentialRepository ?? throw new ArgumentNullException(nameof(userCredentialRepository));
        _environmentConfigRepository = environmentConfigRepository ?? throw new ArgumentNullException(nameof(environmentConfigRepository));
        _usageLogRepository = usageLogRepository ?? throw new ArgumentNullException(nameof(usageLogRepository));
        _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
        _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
        _brokerRegistry = brokerRegistry ?? throw new ArgumentNullException(nameof(brokerRegistry));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ConnectionDto>> Handle(GetConnectionsQuery query, CancellationToken ct)
    {
        var connections = await _connectionRepository.GetAllAsync(ct);
        var accessible = await _accessService.FilterAccessibleAsync(
            connections,
            query.ActorId,
            query.ActorEmail,
            query.ActorRoles ?? Array.Empty<string>(),
            ct);

        return accessible
            .Select(item => MapToDto(item.Connection, item.CanUse, item.CanManage))
            .ToList();
    }

    public async Task<ConnectionDto?> Handle(GetConnectionByIdQuery query, CancellationToken ct)
    {
        var connection = await _connectionRepository.GetByIdOrNameAsync(query.Id, ct);
        if (connection == null)
        {
            return null;
        }

        var roles = query.ActorRoles ?? Array.Empty<string>();
        bool canUse = await _accessService.CanUseAsync(connection, query.ActorId, query.ActorEmail, roles, ct);
        if (!canUse)
        {
            throw new ConnectionAccessDeniedException(
                connection.Id,
                $"Actor '{query.ActorId}' does not have CanUse permission for connection '{connection.Name}'.");
        }

        bool canManage = await _accessService.CanManageAsync(connection, query.ActorId, query.ActorEmail, roles, ct);
        return MapToDto(connection, canUse, canManage);
    }

    public async Task<ConnectionDto> Handle(CreateConnectionCommand command, CancellationToken ct)
    {
        var existingByName = await _connectionRepository.GetByNameAsync(command.Name.Trim(), ct);
        if (existingByName != null)
        {
            throw new DuplicateResourceException($"A connection named '{command.Name.Trim()}' already exists.");
        }

        string allowedHostsJson = JsonSerializer.Serialize(command.AllowedHosts ?? new List<string>());
        string? defaultHeadersJson = command.DefaultHeaders is { Count: > 0 }
            ? JsonSerializer.Serialize(command.DefaultHeaders)
            : null;

        string? encryptedCredentials = !string.IsNullOrWhiteSpace(command.CredentialsJson)
            ? _secretProtector.Protect(command.CredentialsJson, SecretPurposes.Connections)
            : null;

        var entity = new Connection(
            command.Id ?? string.Empty,
            command.Name,
            command.BaseUrl ?? string.Empty,
            string.IsNullOrWhiteSpace(command.AuthType) ? "none" : command.AuthType,
            command.SecretRef,
            allowedHostsJson,
            defaultHeadersJson,
            command.ActorId,
            serviceType: string.IsNullOrWhiteSpace(command.ServiceType) ? "rest" : command.ServiceType,
            authMode: string.IsNullOrWhiteSpace(command.AuthMode) ? "static" : command.AuthMode,
            secretSource: string.IsNullOrWhiteSpace(command.SecretSource) ? "internal_encrypted" : command.SecretSource,
            encryptedCredentialsJson: encryptedCredentials,
            externalSecretKey: command.ExternalSecretKey,
            allowWrite: command.AllowWrite,
            maxRows: command.MaxRows,
            secretExpiresAt: command.SecretExpiresAt);

        _connectionRepository.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        if (encryptedCredentials != null || !string.IsNullOrWhiteSpace(entity.ExternalSecretKey))
        {
            await _auditService.RecordAsync(
                SecurityAuditEventType.SecretCreated,
                command.ActorId,
                "Connection",
                entity.Id,
                new Dictionary<string, object?>
                {
                    ["connectionName"] = entity.Name,
                    ["serviceType"] = entity.ServiceType,
                    ["authMode"] = entity.AuthMode,
                    ["secretSource"] = entity.SecretSource
                },
                ct);
        }

        _logger.LogInformation(
            "AUDIT Connection.Created ConnectionId={ConnectionId} ConnectionName={ConnectionName} ServiceType={ServiceType} AuthMode={AuthMode} Actor={Actor}",
            entity.Id, entity.Name, entity.ServiceType, entity.AuthMode, command.ActorId);

        return MapToDto(entity, canUse: true, canManage: true);
    }

    public async Task<ConnectionDto?> Handle(UpdateConnectionCommand command, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdAsync(command.Id, ct);
        if (entity == null)
        {
            return null;
        }

        var roles = command.ActorRoles ?? Array.Empty<string>();
        bool canManage = await _accessService.CanManageAsync(entity, command.ActorId, command.ActorEmail, roles, ct);
        if (!canManage)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{command.ActorId}' does not have CanManage permission for connection '{entity.Name}'.");
        }

        if (command.ExpectedVersion.HasValue && entity.Version != command.ExpectedVersion.Value)
        {
            throw new ConcurrencyConflictException(
                $"Connection '{entity.Id}' has version {entity.Version}, expected {command.ExpectedVersion.Value}.");
        }

        string trimmedName = command.Name.Trim();
        if (!string.Equals(entity.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = await _connectionRepository.GetByNameAsync(trimmedName, ct);
            if (conflict != null && conflict.Id != entity.Id)
            {
                throw new DuplicateResourceException($"A connection named '{trimmedName}' already exists.");
            }
        }

        string allowedHostsJson = JsonSerializer.Serialize(command.AllowedHosts ?? new List<string>());
        string? defaultHeadersJson = command.DefaultHeaders is { Count: > 0 }
            ? JsonSerializer.Serialize(command.DefaultHeaders)
            : null;

        bool updateEncryptedCredentials = command.ClearCredentials || !string.IsNullOrWhiteSpace(command.CredentialsJson);
        string? encryptedCredentials = !command.ClearCredentials && !string.IsNullOrWhiteSpace(command.CredentialsJson)
            ? _secretProtector.Protect(command.CredentialsJson, SecretPurposes.Connections)
            : null;

        entity.Update(
            command.Name,
            command.BaseUrl ?? entity.BaseUrl,
            string.IsNullOrWhiteSpace(command.AuthType) ? entity.AuthType : command.AuthType,
            command.SecretRef,
            allowedHostsJson,
            defaultHeadersJson,
            serviceType: command.ServiceType,
            authMode: command.AuthMode,
            secretSource: command.SecretSource,
            encryptedCredentialsJson: encryptedCredentials,
            updateEncryptedCredentials: updateEncryptedCredentials,
            externalSecretKey: command.ExternalSecretKey,
            updateExternalSecretKey: command.ExternalSecretKey != null,
            allowWrite: command.AllowWrite,
            maxRows: command.MaxRows);

        await _unitOfWork.SaveChangesAsync(ct);

        if (updateEncryptedCredentials)
        {
            await _auditService.RecordAsync(
                command.ClearCredentials ? SecurityAuditEventType.SecretDeleted : SecurityAuditEventType.SecretRotated,
                command.ActorId,
                "Connection",
                entity.Id,
                new Dictionary<string, object?>
                {
                    ["connectionName"] = entity.Name,
                    ["serviceType"] = entity.ServiceType
                },
                ct);
        }

        _logger.LogInformation(
            "AUDIT Connection.Updated ConnectionId={ConnectionId} ConnectionName={ConnectionName} Version={Version} Actor={Actor}",
            entity.Id, entity.Name, entity.Version, command.ActorId);

        return MapToDto(entity, canUse: true, canManage: true);
    }

    public async Task<bool> Handle(DeleteConnectionCommand command, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdAsync(command.Id, ct);
        if (entity == null)
        {
            return false;
        }

        var roles = command.ActorRoles ?? Array.Empty<string>();
        bool canManage = await _accessService.CanManageAsync(entity, command.ActorId, command.ActorEmail, roles, ct);
        if (!canManage)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{command.ActorId}' does not have CanManage permission to delete connection '{entity.Name}'.");
        }

        string name = entity.Name;
        _connectionRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.RecordAsync(
            SecurityAuditEventType.SecretDeleted,
            command.ActorId,
            "Connection",
            command.Id,
            new Dictionary<string, object?> { ["connectionName"] = name },
            ct);

        _logger.LogInformation(
            "AUDIT Connection.Deleted ConnectionId={ConnectionId} ConnectionName={ConnectionName} Actor={Actor}",
            command.Id, name, command.ActorId);

        return true;
    }

    public async Task<ConnectionTestResult?> Handle(TestConnectionCommand command, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdOrNameAsync(command.Id, ct);
        if (entity == null)
        {
            return null;
        }

        bool canUse = await _accessService.CanUseAsync(entity, command.ActorId, command.ActorEmail, command.ActorRoles, ct);
        if (!canUse)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{command.ActorId}' does not have CanUse permission to test connection '{entity.Name}'.");
        }

        var broker = _brokerRegistry.Resolve(entity.ServiceType);
        if (broker == null)
        {
            return new ConnectionTestResult(false, 0, $"No service broker registered for service type '{entity.ServiceType}'.");
        }

        ConnectionTestResult result;
        try
        {
            var alternates = string.IsNullOrWhiteSpace(command.ActorEmail) ? null : new[] { command.ActorEmail };
            var secret = await _secretResolver.ResolveAsync(entity, command.ActorId, alternates, ct);
            result = await broker.TestConnectionAsync(entity, secret, ct);
        }
        catch (UserCredentialRequiredException)
        {
            result = new ConnectionTestResult(
                false,
                0,
                $"Personal credentials are required for connection '{entity.Name}'. Configure them in your personal vault.");
        }
        catch (ExternalReauthRequiredException ex)
        {
            result = new ConnectionTestResult(false, 0, ex.Message);
        }
        catch (Exception ex)
        {
            result = new ConnectionTestResult(false, 0, ErrorSanitizer.Sanitize(ex.Message));
        }

        // Persist connection health metadata (LastTestedAt, HealthStatus, LastErrorSanitized)
        var tracked = await _connectionRepository.GetByIdAsync(entity.Id, ct);
        if (tracked != null)
        {
            tracked.RecordTestResult(result.Success, result.LatencyMs, result.ErrorMessage);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        await _auditService.RecordAsync(
            SecurityAuditEventType.ConnectionTested,
            command.ActorId,
            "Connection",
            entity.Id,
            new Dictionary<string, object?>
            {
                ["connectionName"] = entity.Name,
                ["serviceType"] = entity.ServiceType,
                ["success"] = result.Success,
                ["latencyMs"] = result.LatencyMs
            },
            ct);

        return result;
    }

    public async Task<ServiceBrokerInvocationResult?> Handle(InvokeConnectionCommand command, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdOrNameAsync(command.Id, ct);
        if (entity == null)
        {
            return null;
        }

        bool canUse = await _accessService.CanUseAsync(entity, command.ActorId, command.ActorEmail, command.ActorRoles, ct);
        if (!canUse)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{command.ActorId}' does not have CanUse permission to invoke connection '{entity.Name}'.");
        }

        var broker = _brokerRegistry.Resolve(entity.ServiceType);
        if (broker == null)
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "BROKER_NOT_FOUND",
                ErrorMessage: $"No service broker registered for service type '{entity.ServiceType}'.");
        }

        var alternates = string.IsNullOrWhiteSpace(command.ActorEmail) ? null : new[] { command.ActorEmail };
        var secret = await _secretResolver.ResolveAsync(entity, command.ActorId, alternates, ct);

        var invocationReq = new ServiceBrokerInvocationRequest(
            Connection: entity,
            Operation: command.Operation,
            Parameters: command.Parameters,
            TableName: command.TableName,
            SchemaName: command.SchemaName,
            Path: command.Path,
            HttpMethod: command.HttpMethod,
            Headers: command.Headers,
            BodyJson: command.BodyJson,
            Secret: secret,
            TimeoutSeconds: command.TimeoutSeconds,
            ActorId: command.ActorEmail ?? command.ActorId);

        return await broker.InvokeAsync(invocationReq, ct);
    }

    public async Task<ConnectionDto?> Handle(RotateConnectionSecretCommand command, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdAsync(command.ConnectionId, ct);
        if (entity == null)
        {
            return null;
        }

        bool canManage = await _accessService.CanManageAsync(entity, command.ActorId, command.ActorEmail, command.ActorRoles, ct);
        if (!canManage)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{command.ActorId}' does not have CanManage permission to rotate secrets for connection '{entity.Name}'.");
        }

        if (command.ExpectedVersion.HasValue && entity.Version != command.ExpectedVersion.Value)
        {
            throw new ConcurrencyConflictException(
                $"Connection '{entity.Id}' has version {entity.Version}, expected {command.ExpectedVersion.Value}.");
        }

        bool updateEncrypted = !string.IsNullOrWhiteSpace(command.CredentialsJson);
        bool updateExternalKey = !string.IsNullOrWhiteSpace(command.ExternalSecretKey);

        if (!updateEncrypted && !updateExternalKey)
        {
            throw new ArgumentException("Either CredentialsJson or ExternalSecretKey must be provided to rotate a secret.");
        }

        string? encryptedPayload = updateEncrypted
            ? _secretProtector.Protect(command.CredentialsJson!, SecretPurposes.Connections)
            : null;

        entity.RotateSecret(
            encryptedCredentialsJson: encryptedPayload,
            updateEncryptedCredentials: updateEncrypted,
            externalSecretKey: command.ExternalSecretKey,
            updateExternalSecretKey: updateExternalKey,
            secretExpiresAt: command.SecretExpiresAt);

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.RecordAsync(
            SecurityAuditEventType.SecretRotated,
            command.ActorEmail ?? command.ActorId,
            "Connection",
            entity.Id,
            new Dictionary<string, object?>
            {
                ["connectionName"] = entity.Name,
                ["serviceType"] = entity.ServiceType,
                ["secretSource"] = entity.SecretSource,
                ["lastRotatedAt"] = entity.LastRotatedAt,
                ["secretExpiresAt"] = entity.SecretExpiresAt
            },
            ct);

        return MapToDto(entity, canUse: true, canManage: true);
    }

    public async Task<ConnectionUsageResponseDto?> Handle(GetConnectionUsageQuery query, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdOrNameAsync(query.ConnectionId, ct);
        if (entity == null)
        {
            return null;
        }

        bool canManage = await _accessService.CanManageAsync(entity, query.ActorId, query.ActorEmail, query.ActorRoles, ct);
        if (!canManage)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{query.ActorId}' does not have CanManage permission to view usage logs for connection '{entity.Name}'.");
        }

        var logs = await _usageLogRepository.GetByConnectionIdAsync(entity.Id, query.Limit, ct);
        var logDtos = logs.Select(l => new ConnectionUsageLogDto(
            l.Id,
            l.ConnectionId,
            l.InstanceId,
            l.WorkflowKey,
            l.NodeId,
            l.ActorId,
            l.Success,
            l.DurationMs,
            l.ExecutedAt)).ToList();

        return new ConnectionUsageResponseDto(
            ConnectionId: entity.Id,
            ConnectionName: entity.Name,
            HealthStatus: entity.HealthStatus,
            LastTestedAt: entity.LastTestedAt,
            LastUsedAt: entity.LastUsedAt,
            LastRotatedAt: entity.LastRotatedAt,
            SecretExpiresAt: entity.SecretExpiresAt,
            LastErrorSanitized: entity.LastErrorSanitized,
            Logs: logDtos);
    }

    public async Task<IReadOnlyList<ConnectionEnvironmentConfigDto>?> Handle(GetConnectionEnvironmentsQuery query, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdOrNameAsync(query.ConnectionId, ct);
        if (entity == null)
        {
            return null;
        }

        bool canManage = await _accessService.CanManageAsync(entity, query.ActorId, query.ActorEmail, query.ActorRoles, ct);
        if (!canManage)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{query.ActorId}' does not have CanManage permission to view environments for connection '{entity.Name}'.");
        }

        var configs = await _environmentConfigRepository.GetByConnectionIdAsync(entity.Id, ct);
        return configs.Select(MapEnvToDto).ToList();
    }

    public async Task<ConnectionEnvironmentConfigDto?> Handle(UpsertConnectionEnvironmentConfigCommand command, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdOrNameAsync(command.ConnectionId, ct);
        if (entity == null)
        {
            return null;
        }

        bool canManage = await _accessService.CanManageAsync(entity, command.ActorId, command.ActorEmail, command.ActorRoles, ct);
        if (!canManage)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{command.ActorId}' does not have CanManage permission to configure environments for connection '{entity.Name}'.");
        }

        string canonicalEnv = ConnectionEnvironmentConfig.NormalizeAndValidateEnvironmentName(command.EnvironmentName);

        bool updateEncrypted = command.ClearCredentialsOverride || !string.IsNullOrWhiteSpace(command.CredentialsJsonOverride);
        string? encryptedOverride = !command.ClearCredentialsOverride && !string.IsNullOrWhiteSpace(command.CredentialsJsonOverride)
            ? _secretProtector.Protect(command.CredentialsJsonOverride!, SecretPurposes.Connections)
            : null;

        var existing = await _environmentConfigRepository.GetByConnectionAndEnvironmentAsync(entity.Id, canonicalEnv, ct);
        if (existing != null)
        {
            existing.Update(
                baseUrlOverride: command.BaseUrlOverride,
                encryptedCredentialsJsonOverride: encryptedOverride,
                updateEncryptedCredentials: updateEncrypted,
                externalSecretKeyOverride: command.ExternalSecretKeyOverride,
                updateExternalSecretKey: command.ExternalSecretKeyOverride != null);
        }
        else
        {
            existing = new ConnectionEnvironmentConfig(
                connectionId: entity.Id,
                environmentName: canonicalEnv,
                baseUrlOverride: command.BaseUrlOverride,
                encryptedCredentialsJsonOverride: encryptedOverride,
                externalSecretKeyOverride: command.ExternalSecretKeyOverride);
            _environmentConfigRepository.Add(existing);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        if (updateEncrypted)
        {
            await _auditService.RecordAsync(
                command.ClearCredentialsOverride ? SecurityAuditEventType.SecretDeleted : SecurityAuditEventType.SecretRotated,
                command.ActorEmail ?? command.ActorId,
                "ConnectionEnvironmentConfig",
                $"{entity.Id}:{canonicalEnv}",
                new Dictionary<string, object?>
                {
                    ["connectionId"] = entity.Id,
                    ["connectionName"] = entity.Name,
                    ["environmentName"] = canonicalEnv
                },
                ct);
        }

        return MapEnvToDto(existing);
    }

    public async Task<IReadOnlyList<ConnectionPermissionDto>?> Handle(GetConnectionPermissionsQuery query, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdOrNameAsync(query.ConnectionId, ct);
        if (entity == null)
        {
            return null;
        }

        bool canManage = await _accessService.CanManageAsync(entity, query.ActorId, query.ActorEmail, query.ActorRoles, ct);
        if (!canManage)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{query.ActorId}' does not have CanManage permission for connection '{entity.Name}'.");
        }

        var perms = await _permissionRepository.GetByConnectionIdAsync(entity.Id, ct);
        return perms.Select(MapPermissionToDto).ToList();
    }

    public async Task<ConnectionPermissionDto?> Handle(UpsertConnectionPermissionCommand command, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdOrNameAsync(command.ConnectionId, ct);
        if (entity == null)
        {
            return null;
        }

        bool canManage = await _accessService.CanManageAsync(entity, command.ActorId, command.ActorEmail, command.ActorRoles, ct);
        if (!canManage)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{command.ActorId}' does not have CanManage permission for connection '{entity.Name}'.");
        }

        var existing = await _permissionRepository.GetBySubjectAsync(entity.Id, command.SubjectType, command.SubjectId, ct);
        if (existing != null)
        {
            existing.UpdatePermissions(command.CanUse, command.CanManage);
        }
        else
        {
            existing = new ConnectionPermission(entity.Id, command.SubjectType, command.SubjectId, command.CanUse, command.CanManage);
            _permissionRepository.Add(existing);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return MapPermissionToDto(existing);
    }

    public async Task<bool?> Handle(DeleteConnectionPermissionCommand command, CancellationToken ct)
    {
        var entity = await _connectionRepository.GetByIdOrNameAsync(command.ConnectionId, ct);
        if (entity == null)
        {
            return null;
        }

        bool canManage = await _accessService.CanManageAsync(entity, command.ActorId, command.ActorEmail, command.ActorRoles, ct);
        if (!canManage)
        {
            throw new ConnectionAccessDeniedException(
                entity.Id,
                $"Actor '{command.ActorId}' does not have CanManage permission for connection '{entity.Name}'.");
        }

        var perm = await _permissionRepository.GetByIdAsync(command.PermissionId, ct);
        if (perm == null || !string.Equals(perm.ConnectionId, entity.Id, StringComparison.Ordinal))
        {
            return false;
        }

        _permissionRepository.Remove(perm);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<UserConnectionCredentialDto>> Handle(GetMyConnectionCredentialsQuery query, CancellationToken ct)
    {
        var allConnections = await _connectionRepository.GetAllAsync(ct);
        var vaultConnections = allConnections
            .Where(c => string.Equals(c.AuthMode, "user_sso_vault", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var existingCreds = await _userCredentialRepository.GetByUserIdAsync(query.UserId, ct);
        var credByConn = existingCreds.ToDictionary(u => u.ConnectionId, StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(query.UserEmail) &&
            !string.Equals(query.UserEmail, query.UserId, StringComparison.OrdinalIgnoreCase))
        {
            var emailCreds = await _userCredentialRepository.GetByUserIdAsync(query.UserEmail, ct);
            foreach (var ec in emailCreds)
            {
                credByConn.TryAdd(ec.ConnectionId, ec);
            }
        }

        return vaultConnections
            .Select(c =>
            {
                bool has = credByConn.TryGetValue(c.Id, out var cred);
                return new UserConnectionCredentialDto(
                    c.Id,
                    c.Name,
                    c.ServiceType,
                    c.AuthMode,
                    HasCredentials: has,
                    UpdatedAt: has ? cred!.UpdatedAt : null);
            })
            .ToList();
    }

    public async Task<UserConnectionCredentialDto?> Handle(UpsertMyConnectionCredentialCommand command, CancellationToken ct)
    {
        var connection = await _connectionRepository.GetByIdOrNameAsync(command.ConnectionId, ct);
        if (connection == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(command.SecretJson))
        {
            throw new ArgumentException("SecretJson is required.", nameof(command));
        }

        string protectedPayload = _secretProtector.Protect(command.SecretJson, SecretPurposes.UserCredentials);

        var existing = await _userCredentialRepository.GetByUserAndConnectionAsync(command.UserId, connection.Id, ct);
        if (existing != null)
        {
            existing.UpdateSecret(protectedPayload);
        }
        else
        {
            existing = new UserConnectionCredential(command.UserId, connection.Id, protectedPayload);
            _userCredentialRepository.Add(existing);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.RecordAsync(
            SecurityAuditEventType.UserCredentialSaved,
            command.UserEmail ?? command.UserId,
            "UserConnectionCredential",
            $"{command.UserId}:{connection.Id}",
            new Dictionary<string, object?>
            {
                ["connectionId"] = connection.Id,
                ["connectionName"] = connection.Name
            },
            ct);

        return new UserConnectionCredentialDto(
            connection.Id,
            connection.Name,
            connection.ServiceType,
            connection.AuthMode,
            HasCredentials: true,
            UpdatedAt: existing.UpdatedAt);
    }

    public async Task<bool> Handle(DeleteMyConnectionCredentialCommand command, CancellationToken ct)
    {
        var connection = await _connectionRepository.GetByIdOrNameAsync(command.ConnectionId, ct);
        string targetConnId = connection?.Id ?? command.ConnectionId;

        var existing = await _userCredentialRepository.GetByUserAndConnectionAsync(command.UserId, targetConnId, ct);
        if (existing == null && !string.IsNullOrWhiteSpace(command.UserEmail))
        {
            existing = await _userCredentialRepository.GetByUserAndConnectionAsync(command.UserEmail, targetConnId, ct);
        }

        if (existing == null)
        {
            return false;
        }

        _userCredentialRepository.Remove(existing);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.RecordAsync(
            SecurityAuditEventType.UserCredentialRevoked,
            command.UserEmail ?? command.UserId,
            "UserConnectionCredential",
            $"{command.UserId}:{targetConnId}",
            new Dictionary<string, object?> { ["connectionId"] = targetConnId },
            ct);

        return true;
    }

    private static ConnectionDto MapToDto(Connection c, bool canUse, bool canManage)
    {
        return new ConnectionDto(
            Id: c.Id,
            Name: c.Name,
            BaseUrl: c.BaseUrl,
            AuthType: c.AuthType,
            SecretRef: c.SecretRef,
            HasSecretRef: !string.IsNullOrWhiteSpace(c.SecretRef),
            AllowedHosts: c.GetAllowedHosts(),
            DefaultHeaders: c.GetDefaultHeaders(),
            Version: c.Version,
            CreatedBy: c.CreatedBy,
            CreatedAt: c.CreatedAt,
            UpdatedAt: c.UpdatedAt,
            ServiceType: c.ServiceType,
            AuthMode: c.AuthMode,
            SecretSource: c.SecretSource,
            HasEncryptedCredentials: !string.IsNullOrWhiteSpace(c.EncryptedCredentialsJson),
            ExternalSecretKey: c.ExternalSecretKey,
            CanUse: canUse,
            CanManage: canManage,
            AllowWrite: c.AllowWrite,
            MaxRows: c.MaxRows,
            SecretExpiresAt: c.SecretExpiresAt,
            LastRotatedAt: c.LastRotatedAt,
            HealthStatus: c.HealthStatus,
            LastTestedAt: c.LastTestedAt,
            LastUsedAt: c.LastUsedAt,
            LastErrorSanitized: c.LastErrorSanitized
        );
    }

    private static ConnectionEnvironmentConfigDto MapEnvToDto(ConnectionEnvironmentConfig e)
    {
        return new ConnectionEnvironmentConfigDto(
            Id: e.Id,
            ConnectionId: e.ConnectionId,
            EnvironmentName: e.EnvironmentName,
            BaseUrlOverride: e.BaseUrlOverride,
            HasEncryptedCredentialsOverride: !string.IsNullOrWhiteSpace(e.EncryptedCredentialsJsonOverride),
            ExternalSecretKeyOverride: e.ExternalSecretKeyOverride,
            UpdatedAt: e.UpdatedAt);
    }

    private static ConnectionPermissionDto MapPermissionToDto(ConnectionPermission p)
    {
        return new ConnectionPermissionDto(
            p.Id,
            p.ConnectionId,
            p.SubjectType,
            p.SubjectId,
            p.CanUse,
            p.CanManage,
            p.CreatedAt,
            p.UpdatedAt);
    }
}
