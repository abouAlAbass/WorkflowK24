using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Api.Auth;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Api.Endpoints;

public sealed record AuthProviderDto(
    string Id,
    string Name,
    string Protocol,
    string Authority,
    string ClientId,
    bool HasSecret,
    string Scopes,
    string RoleMappingJson,
    bool JitProvisioningEnabled,
    bool AllowEmailMatching,
    bool AllowAdminRoleMapping,
    string DefaultRole,
    bool IsEnabled,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record PublicAuthProviderDto(string Id, string Name, string Protocol);

public sealed record PublicAuthProvidersResponse(
    bool LocalLoginEnabled,
    IReadOnlyList<PublicAuthProviderDto> Providers
);

public sealed record UpsertAuthProviderRequest(
    string Name,
    string? Protocol,
    string Authority,
    string ClientId,
    string? ClientSecret = null,
    bool ClearClientSecret = false,
    string? Scopes = null,
    string? RoleMappingJson = null,
    bool JitProvisioningEnabled = true,
    bool AllowEmailMatching = false,
    bool AllowAdminRoleMapping = false,
    string? DefaultRole = null,
    bool IsEnabled = true,
    int? ExpectedVersion = null
);

public sealed record ExternalExchangeRequest(string ExchangeCode);

public static class ExternalAuthEndpoints
{
    public static IEndpointRouteBuilder MapExternalAuthEndpoints(this IEndpointRouteBuilder app)
    {
        MapAdminAuthProviderEndpoints(app);
        MapPublicExternalAuthEndpoints(app);
        return app;
    }

    private static void MapAdminAuthProviderEndpoints(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/auth-providers")
            .WithTags("Auth Providers (Admin)")
            .RequireAuthorization(Permissions.UsersManage);

        admin.MapGet("/", async (IAuthProviderRepository repo, CancellationToken ct) =>
        {
            var providers = await repo.GetAllAsync(ct);
            return Results.Ok(providers.Select(ToDto).ToList());
        })
        .WithName("GetAdminAuthProviders")
        .WithSummary("Lists all configured OIDC auth providers without exposing encrypted or plaintext secrets.");

        admin.MapPost("/", async (
            UpsertAuthProviderRequest req,
            ClaimsPrincipal principal,
            IAuthProviderRepository repo,
            ISecretProtector protector,
            ISsrfGuard ssrfGuard,
            IAuditService audit,
            CancellationToken ct) =>
        {
            var validationError = await ValidateProviderRequestAsync(req, ssrfGuard, ct);
            if (validationError != null) return validationError;

            var existingByName = await repo.GetByNameAsync(req.Name, ct);
            if (existingByName != null)
            {
                return Results.Conflict(new { code = "DUPLICATE_PROVIDER_NAME", error = $"An auth provider named '{req.Name}' already exists." });
            }

            string? encryptedSecret = !string.IsNullOrWhiteSpace(req.ClientSecret)
                ? protector.Protect(req.ClientSecret, SecretPurposes.AuthProviders)
                : null;

            var entity = new AuthProvider(
                id: null,
                name: req.Name,
                protocol: req.Protocol ?? AuthProvider.DefaultProtocol,
                authority: req.Authority,
                clientId: req.ClientId,
                encryptedClientSecret: encryptedSecret,
                scopes: req.Scopes,
                roleMappingJson: req.RoleMappingJson,
                jitProvisioningEnabled: req.JitProvisioningEnabled,
                allowEmailMatching: req.AllowEmailMatching,
                allowAdminRoleMapping: req.AllowAdminRoleMapping,
                defaultRole: req.DefaultRole,
                isEnabled: req.IsEnabled);

            await repo.AddAsync(entity, ct);

            await audit.RecordAsync(
                SecurityAuditEventType.AuthProviderChanged,
                CurrentActor.Id(principal),
                "AuthProvider",
                entity.Id,
                new Dictionary<string, object?>
                {
                    ["action"] = "created",
                    ["name"] = entity.Name,
                    ["authority"] = entity.Authority,
                    ["isEnabled"] = entity.IsEnabled,
                    ["allowAdminRoleMapping"] = entity.AllowAdminRoleMapping,
                    ["hasSecret"] = !string.IsNullOrWhiteSpace(entity.EncryptedClientSecret)
                },
                ct);

            return Results.Created($"/api/admin/auth-providers/{entity.Id}", ToDto(entity));
        })
        .WithName("CreateAuthProvider")
        .WithSummary("Creates a new OIDC auth provider after SSRF and admin-mapping validation.");

        admin.MapPut("/{id}", async (
            string id,
            UpsertAuthProviderRequest req,
            ClaimsPrincipal principal,
            IAuthProviderRepository repo,
            ISecretProtector protector,
            ISsrfGuard ssrfGuard,
            IOidcDiscoveryCache discoveryCache,
            IAuditService audit,
            CancellationToken ct) =>
        {
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing == null)
            {
                return Results.NotFound(new { error = $"Auth provider '{id}' not found." });
            }

            if (req.ExpectedVersion.HasValue && existing.Version != req.ExpectedVersion.Value)
            {
                return Results.Conflict(new
                {
                    code = "CONCURRENT_MODIFICATION",
                    error = $"Auth provider '{id}' was modified concurrently (expected version {req.ExpectedVersion.Value}, current {existing.Version})."
                });
            }

            var validationError = await ValidateProviderRequestAsync(req, ssrfGuard, ct);
            if (validationError != null) return validationError;

            string oldAuthority = existing.Authority;
            bool updateSecret = req.ClearClientSecret || !string.IsNullOrWhiteSpace(req.ClientSecret);
            string? encryptedSecret = req.ClearClientSecret
                ? null
                : (!string.IsNullOrWhiteSpace(req.ClientSecret)
                    ? protector.Protect(req.ClientSecret, SecretPurposes.AuthProviders)
                    : null);

            existing.Update(
                name: req.Name,
                authority: req.Authority,
                clientId: req.ClientId,
                encryptedClientSecret: encryptedSecret,
                updateSecret: updateSecret,
                scopes: req.Scopes,
                roleMappingJson: req.RoleMappingJson,
                jitProvisioningEnabled: req.JitProvisioningEnabled,
                allowEmailMatching: req.AllowEmailMatching,
                allowAdminRoleMapping: req.AllowAdminRoleMapping,
                defaultRole: req.DefaultRole,
                isEnabled: req.IsEnabled);

            try
            {
                await repo.UpdateAsync(existing, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new
                {
                    code = "CONCURRENT_MODIFICATION",
                    error = $"Auth provider '{id}' was modified concurrently."
                });
            }

            discoveryCache.Invalidate(oldAuthority);
            discoveryCache.Invalidate(existing.Authority);

            await audit.RecordAsync(
                SecurityAuditEventType.AuthProviderChanged,
                CurrentActor.Id(principal),
                "AuthProvider",
                existing.Id,
                new Dictionary<string, object?>
                {
                    ["action"] = "updated",
                    ["name"] = existing.Name,
                    ["authority"] = existing.Authority,
                    ["isEnabled"] = existing.IsEnabled,
                    ["allowAdminRoleMapping"] = existing.AllowAdminRoleMapping,
                    ["hasSecret"] = !string.IsNullOrWhiteSpace(existing.EncryptedClientSecret)
                },
                ct);

            return Results.Ok(ToDto(existing));
        })
        .WithName("UpdateAuthProvider")
        .WithSummary("Updates an existing OIDC auth provider with optimistic concurrency and SSRF checks.");

        admin.MapDelete("/{id}", async (
            string id,
            ClaimsPrincipal principal,
            IAuthProviderRepository repo,
            IOidcDiscoveryCache discoveryCache,
            IAuditService audit,
            CancellationToken ct) =>
        {
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing == null)
            {
                return Results.NotFound(new { error = $"Auth provider '{id}' not found." });
            }

            await repo.DeleteAsync(id, ct);
            discoveryCache.Invalidate(existing.Authority);

            await audit.RecordAsync(
                SecurityAuditEventType.AuthProviderChanged,
                CurrentActor.Id(principal),
                "AuthProvider",
                existing.Id,
                new Dictionary<string, object?>
                {
                    ["action"] = "deleted",
                    ["name"] = existing.Name,
                    ["authority"] = existing.Authority
                },
                ct);

            return Results.NoContent();
        })
        .WithName("DeleteAuthProvider")
        .WithSummary("Deletes an OIDC auth provider and invalidates its discovery/JWKS cache.");
    }

    private static void MapPublicExternalAuthEndpoints(IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        auth.MapGet("/providers", async (IAuthProviderRepository repo, CancellationToken ct) =>
        {
            var enabled = await repo.GetEnabledAsync(ct);
            var list = enabled.Select(p => new PublicAuthProviderDto(p.Id, p.Name, p.Protocol)).ToList();
            return Results.Ok(new PublicAuthProvidersResponse(LocalLoginEnabled: true, Providers: list));
        })
        .AllowAnonymous()
        .WithName("GetPublicAuthProviders")
        .WithSummary("Lists enabled external OIDC providers and confirms local break-glass login availability.");

        auth.MapGet("/external/{providerId}/challenge", async (
            string providerId,
            string? returnUrl,
            HttpContext httpContext,
            IOidcAuthenticationService oidcService,
            CancellationToken ct) =>
        {
            string callbackUri = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/auth/external/{Uri.EscapeDataString(providerId)}/callback";
            var result = await oidcService.StartChallengeAsync(providerId, callbackUri, returnUrl, ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.AuthorizationUrl))
            {
                return Results.Json(
                    new { code = result.ErrorCode, error = result.ErrorMessage },
                    statusCode: result.StatusCode);
            }

            return Results.Redirect(result.AuthorizationUrl, permanent: false);
        })
        .AllowAnonymous()
        .WithName("ExternalAuthChallenge")
        .WithSummary("Starts an OIDC Authorization Code + PKCE (S256) + nonce flow and redirects (302) to the IdP.");

        auth.MapGet("/external/{providerId}/callback", async (
            string providerId,
            string? code,
            string? state,
            string? error,
            IOidcAuthenticationService oidcService,
            CancellationToken ct) =>
        {
            var result = await oidcService.HandleCallbackAsync(providerId, code, state, error, ct);
            if (!result.Success || string.IsNullOrWhiteSpace(result.FrontendRedirectUrl))
            {
                return Results.Json(
                    new { code = result.ErrorCode, error = result.ErrorMessage },
                    statusCode: result.StatusCode);
            }

            return Results.Redirect(result.FrontendRedirectUrl, permanent: false);
        })
        .AllowAnonymous()
        .WithName("ExternalAuthCallback")
        .WithSummary("Validates OIDC callback, links/provisions user, and redirects with an opaque one-time exchangeCode.");

        auth.MapPost("/external/exchange", async (
            ExternalExchangeRequest req,
            IOidcFlowStateStore stateStore,
            ISystemUserRepository users,
            IJwtTokenService tokenService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.ExchangeCode))
            {
                return Results.BadRequest(new { code = "INVALID_EXCHANGE_CODE", error = "ExchangeCode is required." });
            }

            var payload = stateStore.ConsumeExchangeCode(req.ExchangeCode);
            if (payload == null)
            {
                return Results.BadRequest(new { code = "INVALID_EXCHANGE_CODE", error = "Exchange code is invalid, expired, or already used." });
            }

            var user = await users.GetByIdAsync(payload.UserId, ct);
            if (user == null || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { code = "USER_INACTIVE", error = "User account is inactive or no longer exists." }, statusCode: StatusCodes.Status403Forbidden);
            }

            string token = tokenService.GenerateToken(user.Id, user.Email, user.Role, user.Group);
            return Results.Ok(new LoginResponse(token, user.Id, user.Name, user.Email, user.Role, user.Group));
        })
        .AllowAnonymous()
        .RequireRateLimiting("AuthPolicy")
        .WithName("ExternalAuthExchange")
        .WithSummary("Consumes a one-time opaque exchangeCode and returns a Tamam JWT LoginResponse.");
    }

    private static async Task<IResult?> ValidateProviderRequestAsync(
        UpsertAuthProviderRequest req,
        ISsrfGuard ssrfGuard,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Results.BadRequest(new { code = "VALIDATION_ERROR", error = "Provider name is required." });
        }

        if (string.IsNullOrWhiteSpace(req.ClientId))
        {
            return Results.BadRequest(new { code = "VALIDATION_ERROR", error = "ClientId is required." });
        }

        var protocol = string.IsNullOrWhiteSpace(req.Protocol) ? AuthProvider.DefaultProtocol : req.Protocol.Trim().ToLowerInvariant();
        if (!string.Equals(protocol, AuthProvider.DefaultProtocol, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { code = "UNSUPPORTED_PROTOCOL", error = "Only 'oidc' protocol is supported." });
        }

        if (!string.IsNullOrWhiteSpace(req.DefaultRole) && AuthProvider.IsAdminRole(req.DefaultRole))
        {
            return Results.BadRequest(new { code = "ADMIN_DEFAULT_ROLE_FORBIDDEN", error = "DefaultRole cannot be an Admin role." });
        }

        if (!OidcRoleMapper.TryValidateRoleMapping(req.RoleMappingJson, req.AllowAdminRoleMapping, out _, out var roleCode, out var roleErr))
        {
            return Results.BadRequest(new { code = roleCode, error = roleErr });
        }

        if (string.IsNullOrWhiteSpace(req.Authority) ||
            !Uri.TryCreate(req.Authority.Trim(), UriKind.Absolute, out var authorityUri) ||
            !string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { code = "INVALID_AUTHORITY", error = "Authority must be a valid HTTPS URL." });
        }

        var ssrf = await ssrfGuard.ValidateAsync(authorityUri, new[] { authorityUri.DnsSafeHost }, ct);
        if (!ssrf.IsAllowed)
        {
            return Results.BadRequest(new { code = "SSRF_BLOCKED", error = ssrf.BlockReason });
        }

        return null;
    }

    private static AuthProviderDto ToDto(AuthProvider p) => new(
        Id: p.Id,
        Name: p.Name,
        Protocol: p.Protocol,
        Authority: p.Authority,
        ClientId: p.ClientId,
        HasSecret: !string.IsNullOrWhiteSpace(p.EncryptedClientSecret),
        Scopes: p.Scopes,
        RoleMappingJson: p.RoleMappingJson,
        JitProvisioningEnabled: p.JitProvisioningEnabled,
        AllowEmailMatching: p.AllowEmailMatching,
        AllowAdminRoleMapping: p.AllowAdminRoleMapping,
        DefaultRole: p.DefaultRole,
        IsEnabled: p.IsEnabled,
        Version: p.Version,
        CreatedAt: p.CreatedAt,
        UpdatedAt: p.UpdatedAt
    );
}
