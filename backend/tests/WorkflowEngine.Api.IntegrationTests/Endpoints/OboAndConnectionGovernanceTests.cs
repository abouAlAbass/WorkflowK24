using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WorkflowEngine.Api.Auth;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Features.Connections.DTOs;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Services;
using WorkflowEngine.Infrastructure.Services.Brokers;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

#region Test Doubles for E.3 OBO & Connection Governance

public sealed class OboGovernanceDnsResolver : IDnsResolver
{
    private readonly Dictionary<string, IPAddress[]> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["idp.obo.test"] = new[] { IPAddress.Parse("93.184.216.40") },
        ["graph.microsoft.com"] = new[] { IPAddress.Parse("20.190.151.68") },
        ["login.microsoftonline.com"] = new[] { IPAddress.Parse("20.190.151.69") },
        ["dev.partner.test"] = new[] { IPAddress.Parse("93.184.216.41") },
        ["prod.partner.test"] = new[] { IPAddress.Parse("93.184.216.42") }
    };

    public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct = default)
    {
        if (_map.TryGetValue(host, out var ips))
            return Task.FromResult(ips);
        return Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
    }
}

public sealed class MockOboIdpHandler : HttpMessageHandler
{
    private readonly RSA _rsa = RSA.Create(2048);
    private const string Kid = "obo-key-v1";

    public ConcurrentQueue<Dictionary<string, string>> TokenFormRequests { get; } = new();
    public Func<Dictionary<string, string>, string>? IdTokenFactory { get; set; }
    public string InitialAccessToken { get; set; } = "idp-external-access-secret-v1";
    public string InitialRefreshToken { get; set; } = "idp-external-refresh-secret-v1";
    public string RefreshedAccessToken { get; set; } = "idp-refreshed-access-secret-v2";
    public string OboGraphAccessToken { get; set; } = "graph-obo-delegated-token-user42";

    public string SignIdToken(string issuer, string audience, string subject, string nonce, string email, string name)
    {
        var key = new RsaSecurityKey(_rsa.ExportParameters(true)) { KeyId = Kid };
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Nonce, nonce),
            new(JwtRegisteredClaimNames.Email, email),
            new("email_verified", "true"),
            new("name", name),
            new("groups", "Designers")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        string authority = $"{uri.Scheme}://{uri.Authority}";

        if (uri.AbsolutePath.EndsWith("/.well-known/openid-configuration", StringComparison.OrdinalIgnoreCase))
        {
            var discovery = JsonSerializer.Serialize(new
            {
                issuer = authority,
                authorization_endpoint = $"{authority}/authorize",
                token_endpoint = $"{authority}/token",
                jwks_uri = $"{authority}/jwks"
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(discovery, Encoding.UTF8, "application/json")
            };
        }

        if (uri.AbsolutePath.EndsWith("/jwks", StringComparison.OrdinalIgnoreCase))
        {
            var pub = _rsa.ExportParameters(false);
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(pub) { KeyId = Kid });
            var jwksJson = JsonSerializer.Serialize(new
            {
                keys = new[]
                {
                    new { kty = jwk.Kty, use = "sig", kid = jwk.Kid, alg = SecurityAlgorithms.RsaSha256, n = jwk.N, e = jwk.E }
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jwksJson, Encoding.UTF8, "application/json")
            };
        }

        if (uri.AbsolutePath.EndsWith("/token", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
        {
            string body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            var parsed = HttpUtility.ParseQueryString(body);
            var form = parsed.AllKeys
                .Where(k => k != null)
                .ToDictionary(k => k!, k => parsed[k!] ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            TokenFormRequests.Enqueue(form);

            string grantType = form.GetValueOrDefault("grant_type") ?? string.Empty;

            if (string.Equals(grantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
            {
                string idToken = IdTokenFactory?.Invoke(form)
                    ?? throw new InvalidOperationException("IdTokenFactory not set.");
                var json = JsonSerializer.Serialize(new
                {
                    access_token = InitialAccessToken,
                    refresh_token = InitialRefreshToken,
                    token_type = "Bearer",
                    expires_in = 3600,
                    id_token = idToken
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            if (string.Equals(grantType, "refresh_token", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(new
                {
                    access_token = RefreshedAccessToken,
                    refresh_token = InitialRefreshToken,
                    token_type = "Bearer",
                    expires_in = 3600
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            if (string.Equals(grantType, OboTokenExchangeService.OboGrantType, StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(new
                {
                    access_token = OboGraphAccessToken,
                    token_type = "Bearer",
                    expires_in = 3600,
                    scope = form.GetValueOrDefault("scope")
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

public sealed class RecordingHttpConnectorTransport : IHttpConnectorTransport
{
    public ConcurrentQueue<(Uri Uri, string? AuthorizationHeader)> CapturedRequests { get; } = new();

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken ct = default)
    {
        CapturedRequests.Enqueue((request.RequestUri!, request.Headers.Authorization?.ToString()));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":"ok","env":"resolved"}""", Encoding.UTF8, "application/json")
        });
    }
}

#endregion

public class OboGovernanceTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _testDbName = $"WorkflowEngineOboGovTestDb_{Guid.NewGuid():N}";
    private readonly string _tamamEnvironment;

    public MockOboIdpHandler MockIdp { get; } = new();
    public OboGovernanceDnsResolver DnsResolver { get; } = new();
    public StubSharePointHttpMessageHandler GraphHandler { get; } = new();
    public RecordingHttpConnectorTransport RestTransport { get; } = new();

    public OboGovernanceTestFactory() : this("Dev") { }

    internal OboGovernanceTestFactory(string tamamEnvironment)
    {
        _tamamEnvironment = tamamEnvironment;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "SqlServer");
        builder.UseSetting(
            "Database:ConnectionString",
            $"Server=(localdb)\\mssqllocaldb;Database={_testDbName};Trusted_Connection=True;MultipleActiveResultSets=true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "500");
        builder.UseSetting("Seed:PrimaryAdmin:Email", "adel.sebti@corp.internal");
        builder.UseSetting("TAMAM_ENVIRONMENT", _tamamEnvironment);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDnsResolver>();
            services.AddSingleton<IDnsResolver>(DnsResolver);

            services.RemoveAll<IHttpConnectorTransport>();
            services.AddSingleton<IHttpConnectorTransport>(RestTransport);

            services.AddHttpClient(OidcHttpClientNames.OidcProvider)
                .ConfigurePrimaryHttpMessageHandler(() => MockIdp);

            services.AddHttpClient(OboTokenExchangeService.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => MockIdp);

            services.AddHttpClient(SharePointServiceBroker.GraphHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => GraphHandler);
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
        catch
        {
            // Ignore cleanup errors
        }

        await base.DisposeAsync();
    }
}

public class OboAndConnectionGovernanceTests : IClassFixture<OboGovernanceTestFactory>
{
    private readonly OboGovernanceTestFactory _factory;

    public OboAndConnectionGovernanceTests(OboGovernanceTestFactory factory)
    {
        _factory = factory;
    }

    private static async Task<HttpClient> CreateAdminClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("adel.sebti@corp.internal", DatabaseInitializer.DefaultSeedPassword));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private static async Task<LoginResponse> PerformOidcLoginAsync(
        OboGovernanceTestFactory factory,
        string providerId,
        string authority,
        string clientId,
        string subject,
        string email,
        string displayName)
    {
        var noRedirectClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var challengeResp = await noRedirectClient.GetAsync($"/api/auth/external/{providerId}/challenge");
        challengeResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var authUri = challengeResp.Headers.Location!;
        var qs = HttpUtility.ParseQueryString(authUri.Query);
        string state = qs["state"]!;
        string nonce = qs["nonce"]!;

        factory.MockIdp.IdTokenFactory = _ => factory.MockIdp.SignIdToken(authority, clientId, subject, nonce, email, displayName);

        var callbackResp = await noRedirectClient.GetAsync($"/api/auth/external/{providerId}/callback?code=auth-code-123&state={Uri.EscapeDataString(state)}");
        callbackResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var cbLocation = callbackResp.Headers.Location!;
        string exchangeCode = HttpUtility.ParseQueryString(cbLocation.Query)["exchangeCode"]!;

        var exchangeResp = await noRedirectClient.PostAsJsonAsync("/api/auth/external/exchange", new ExternalExchangeRequest(exchangeCode));
        exchangeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await exchangeResp.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    [Fact]
    public async Task ExternalTokens_StoredEncryptedWithDedicatedPurpose_AndNeverIncludedInInternalJwt()
    {
        var admin = await CreateAdminClientAsync(_factory);
        const string authority = "https://idp.obo.test";
        const string clientId = "tamam-oidc-client";

        var createProviderResp = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: $"EntraOBO-{Guid.NewGuid():N}"[..24],
            Protocol: "oidc",
            Authority: authority,
            ClientId: clientId,
            ClientSecret: "idp-client-secret-top-secret",
            Scopes: "openid profile email offline_access",
            RoleMappingJson: """{"Designers":"Designer"}""",
            JitProvisioningEnabled: true,
            AllowEmailMatching: true,
            AllowAdminRoleMapping: false,
            DefaultRole: "Designer",
            IsEnabled: true));
        createProviderResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var provider = await createProviderResp.Content.ReadFromJsonAsync<AuthProviderDto>();

        var login = await PerformOidcLoginAsync(
            _factory,
            provider!.Id,
            authority,
            clientId,
            subject: "sub-alice-obo-1",
            email: "alice.obo@corp.internal",
            displayName: "Alice OBO");

        // 1. Internal JWT must NEVER contain external access or refresh tokens
        login.Token.Should().NotContain(_factory.MockIdp.InitialAccessToken);
        login.Token.Should().NotContain(_factory.MockIdp.InitialRefreshToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.Token);
        jwt.Claims.Select(c => c.Value).Should().NotContain(_factory.MockIdp.InitialAccessToken);
        jwt.Claims.Select(c => c.Value).Should().NotContain(_factory.MockIdp.InitialRefreshToken);
        jwt.Claims.Select(c => c.Type).Should().NotContain(t => t.Contains("access_token", StringComparison.OrdinalIgnoreCase));

        // 2. ExternalUserToken stored in DB encrypted strictly under SecretPurposes.ExternalTokens
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var stored = await db.ExternalUserTokens.FirstOrDefaultAsync(t => t.UserId == login.UserId && t.ProviderId == provider.Id);
        stored.Should().NotBeNull();
        stored!.EncryptedAccessToken.Should().NotContain(_factory.MockIdp.InitialAccessToken);
        stored.EncryptedRefreshToken.Should().NotBeNullOrWhiteSpace();
        stored.EncryptedRefreshToken!.Should().NotContain(_factory.MockIdp.InitialRefreshToken);

        // Decryption with dedicated purpose succeeds
        protector.Unprotect(stored.EncryptedAccessToken, SecretPurposes.ExternalTokens)
            .Should().Be(_factory.MockIdp.InitialAccessToken);
        protector.Unprotect(stored.EncryptedRefreshToken!, SecretPurposes.ExternalTokens)
            .Should().Be(_factory.MockIdp.InitialRefreshToken);

        // Decryption with any wrong purpose MUST fail with CryptographicException
        Action wrongPurpose = () => protector.Unprotect(stored.EncryptedAccessToken, SecretPurposes.Connections);
        wrongPurpose.Should().Throw<CryptographicException>();
    }

    [Fact]
    public async Task SharePointOboMode_ExchangesUserTokenAndInvokesGraphOnBehalfOfUser()
    {
        var admin = await CreateAdminClientAsync(_factory);
        const string authority = "https://idp.obo.test";
        const string clientId = "tamam-sp-obo-client";

        var createProviderResp = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: $"EntraSP-{Guid.NewGuid():N}"[..24],
            Protocol: "oidc",
            Authority: authority,
            ClientId: clientId,
            ClientSecret: "sp-obo-client-secret",
            Scopes: "openid profile email offline_access",
            RoleMappingJson: """{"Designers":"Designer"}""",
            JitProvisioningEnabled: true,
            AllowEmailMatching: true,
            AllowAdminRoleMapping: false,
            DefaultRole: "Designer",
            IsEnabled: true));
        createProviderResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var provider = await createProviderResp.Content.ReadFromJsonAsync<AuthProviderDto>();

        // Create SharePoint connection configured with AuthMode = on_behalf_of
        var createConnResp = await admin.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            Id: null,
            Name: $"SP_OBO_{Guid.NewGuid():N}"[..18],
            BaseUrl: "https://graph.microsoft.com/v1.0/sites/contoso.sharepoint.com,site-obo",
            AuthType: "bearer",
            SecretRef: null,
            AllowedHosts: new List<string> { "graph.microsoft.com", "idp.obo.test" },
            DefaultHeaders: null,
            ServiceType: "sharepoint",
            AuthMode: "on_behalf_of",
            SecretSource: "internal_encrypted",
            CredentialsJson: JsonSerializer.Serialize(new
            {
                providerId = provider!.Id,
                scope = "https://graph.microsoft.com/Sites.Read.All"
            }),
            AllowWrite: false,
            MaxRows: 50));
        createConnResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var conn = await createConnResp.Content.ReadFromJsonAsync<ConnectionDto>();

        // 1. Invoking as Admin (who has NO ExternalUserToken) MUST return 422 EXTERNAL_REAUTH_REQUIRED
        var noTokenInvoke = await admin.PostAsJsonAsync($"/api/connections/{conn!.Id}/invoke", new InvokeConnectionRequest(
            Operation: "list",
            TableName: "Contracts"));
        noTokenInvoke.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errBody = await noTokenInvoke.Content.ReadAsStringAsync();
        errBody.Should().Contain("EXTERNAL_REAUTH_REQUIRED");

        // 2. Authenticate external OIDC user and grant CanUse on the SharePoint connection
        var oidcLogin = await PerformOidcLoginAsync(
            _factory,
            provider.Id,
            authority,
            clientId,
            subject: "sub-bob-obo-2",
            email: "bob.obo@corp.internal",
            displayName: "Bob OBO");

        var permResp = await admin.PutAsJsonAsync($"/api/connections/{conn.Id}/permissions", new UpsertConnectionPermissionRequest(
            SubjectType: "User",
            SubjectId: oidcLogin.Email,
            CanUse: true,
            CanManage: false));
        permResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Expire the stored access token to also verify automatic refresh via EncryptedRefreshToken before OBO exchange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var tok = await db.ExternalUserTokens.FirstAsync(t => t.UserId == oidcLogin.UserId && t.ProviderId == provider.Id);
            tok.UpdateTokens(tok.EncryptedAccessToken, tok.EncryptedRefreshToken, DateTime.UtcNow.AddMinutes(-5));
            await db.SaveChangesAsync();
        }

        // Stub Microsoft Graph response
        _factory.GraphHandler.Reset();
        string? receivedGraphAuth = null;
        _factory.GraphHandler.Handler = (req, _) =>
        {
            receivedGraphAuth = req.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"value":[{"id":"101","fields":{"Title":"NDA-2026"}}]}""", Encoding.UTF8, "application/json")
            };
        };

        // 3. Invoke SharePoint connection as Bob (external OIDC user)
        var bobClient = _factory.CreateClient();
        bobClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oidcLogin.Token);

        var oboInvoke = await bobClient.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new InvokeConnectionRequest(
            Operation: "list",
            TableName: "Contracts"));
        oboInvoke.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await oboInvoke.Content.ReadFromJsonAsync<ServiceBrokerInvocationResult>();
        result!.Success.Should().BeTrue();
        result.Rows.Should().NotBeNull().And.HaveCount(1);

        // Verify refresh_token + OBO jwt-bearer requests were sent to IdP
        var tokenForms = _factory.MockIdp.TokenFormRequests.ToList();
        tokenForms.Should().Contain(f => f.GetValueOrDefault("grant_type") == "refresh_token");
        tokenForms.Should().Contain(f =>
            f.GetValueOrDefault("grant_type") == OboTokenExchangeService.OboGrantType &&
            f.GetValueOrDefault("requested_token_use") == "on_behalf_of" &&
            f.GetValueOrDefault("assertion") == _factory.MockIdp.RefreshedAccessToken &&
            f.GetValueOrDefault("scope") == "https://graph.microsoft.com/Sites.Read.All");

        // Verify Microsoft Graph received the OBO-exchanged token
        receivedGraphAuth.Should().Be($"Bearer {_factory.MockIdp.OboGraphAccessToken}");
    }

    [Fact]
    public async Task ConnectionEnvironmentOverrides_ResolvesProdBaseUrlAndSecretWhenInProdEnvironment()
    {
        await using var prodFactory = new OboGovernanceTestFactory("Prod");
        await prodFactory.InitializeAsync();
        var admin = await CreateAdminClientAsync(prodFactory);

        const string devSecret = "dev-api-token-000";
        const string prodSecret = "prod-super-secret-token-999";

        var createResp = await admin.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            Id: null,
            Name: $"ERP_ENV_{Guid.NewGuid():N}"[..18],
            BaseUrl: "https://dev.partner.test",
            AuthType: "bearer",
            SecretRef: null,
            AllowedHosts: new List<string> { "dev.partner.test", "prod.partner.test" },
            DefaultHeaders: null,
            ServiceType: "rest",
            AuthMode: "static",
            SecretSource: "internal_encrypted",
            CredentialsJson: JsonSerializer.Serialize(new { token = devSecret })));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var conn = await createResp.Content.ReadFromJsonAsync<ConnectionDto>();

        // Configure Prod override via PUT /api/connections/{id}/environments
        var putEnvResp = await admin.PutAsJsonAsync($"/api/connections/{conn!.Id}/environments", new UpsertConnectionEnvironmentRequest(
            EnvironmentName: "Prod",
            BaseUrlOverride: "https://prod.partner.test",
            CredentialsJsonOverride: JsonSerializer.Serialize(new { token = prodSecret })));
        putEnvResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var envBody = await putEnvResp.Content.ReadAsStringAsync();
        envBody.Should().NotContain(prodSecret);

        // GET /api/connections/{id}/environments must return metadata only (never secrets)
        var getEnvResp = await admin.GetAsync($"/api/connections/{conn.Id}/environments");
        getEnvResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var getEnvRaw = await getEnvResp.Content.ReadAsStringAsync();
        getEnvRaw.Should().NotContain(prodSecret);
        var envs = await getEnvResp.Content.ReadFromJsonAsync<List<ConnectionEnvironmentConfigDto>>();
        envs.Should().ContainSingle(e =>
            e.EnvironmentName == "Prod" &&
            e.BaseUrlOverride == "https://prod.partner.test" &&
            e.HasEncryptedCredentialsOverride);

        // Invoke connection -> because TAMAM_ENVIRONMENT = Prod, must target prod.partner.test with prodSecret
        var invokeResp = await admin.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new InvokeConnectionRequest(
            Operation: "get",
            Path: "/v1/invoices"));
        invokeResp.StatusCode.Should().Be(HttpStatusCode.OK);

        prodFactory.RestTransport.CapturedRequests.Should().Contain(req =>
            req.Uri.ToString() == "https://prod.partner.test/v1/invoices" &&
            req.AuthorizationHeader == $"Bearer {prodSecret}");
    }

    [Fact]
    public async Task RotateSecretAndUsageLogs_UpdatesMetadataAuditsEventAndLogsZeroSecrets()
    {
        var admin = await CreateAdminClientAsync(_factory);
        const string initialSecret = "initial-secret-alpha-111";
        const string rotatedSecret = "rotated-secret-omega-999";

        var createResp = await admin.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            Id: null,
            Name: $"ROTATE_REST_{Guid.NewGuid():N}"[..20],
            BaseUrl: "https://dev.partner.test",
            AuthType: "bearer",
            SecretRef: null,
            AllowedHosts: new List<string> { "dev.partner.test" },
            DefaultHeaders: null,
            ServiceType: "rest",
            AuthMode: "static",
            SecretSource: "internal_encrypted",
            CredentialsJson: JsonSerializer.Serialize(new { token = initialSecret })));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var conn = await createResp.Content.ReadFromJsonAsync<ConnectionDto>();

        // 1. Invoke once before rotation
        var invoke1 = await admin.PostAsJsonAsync($"/api/connections/{conn!.Id}/invoke", new InvokeConnectionRequest(
            Operation: "get",
            Path: "/ping"));
        invoke1.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Rotate secret with a new expiration date
        var newExpiry = DateTime.UtcNow.AddDays(90);
        var rotateResp = await admin.PostAsJsonAsync($"/api/connections/{conn.Id}/rotate-secret", new RotateConnectionSecretRequest(
            CredentialsJson: JsonSerializer.Serialize(new { token = rotatedSecret }),
            SecretExpiresAt: newExpiry,
            ExpectedVersion: conn.Version));
        rotateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotateRaw = await rotateResp.Content.ReadAsStringAsync();
        rotateRaw.Should().NotContain(initialSecret);
        rotateRaw.Should().NotContain(rotatedSecret);

        var rotated = await rotateResp.Content.ReadFromJsonAsync<ConnectionDto>();
        rotated!.LastRotatedAt.Should().NotBeNull();
        rotated.SecretExpiresAt.Should().NotBeNull();
        rotated.Version.Should().Be(conn.Version + 1);

        // 3. Invoke again after rotation -> outbound call must use rotatedSecret
        var invoke2 = await admin.PostAsJsonAsync($"/api/connections/{conn.Id}/invoke", new InvokeConnectionRequest(
            Operation: "get",
            Path: "/ping-v2"));
        invoke2.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.RestTransport.CapturedRequests.Should().Contain(req =>
            req.Uri.ToString() == "https://dev.partner.test/ping-v2" &&
            req.AuthorizationHeader == $"Bearer {rotatedSecret}");

        // 4. GET /api/connections/{id}/usage?limit=50 must return usage logs & health status with ZERO secrets
        var usageResp = await admin.GetAsync($"/api/connections/{conn.Id}/usage?limit=50");
        usageResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var usageRaw = await usageResp.Content.ReadAsStringAsync();
        usageRaw.Should().NotContain(initialSecret);
        usageRaw.Should().NotContain(rotatedSecret);

        var usage = await usageResp.Content.ReadFromJsonAsync<ConnectionUsageResponseDto>();
        usage.Should().NotBeNull();
        usage!.HealthStatus.Should().Be("Healthy");
        usage.LastUsedAt.Should().NotBeNull();
        usage.LastRotatedAt.Should().NotBeNull();
        usage.Logs.Should().HaveCountGreaterThanOrEqualTo(2);
        usage.Logs.Should().OnlyContain(l => l.Success && l.ConnectionId == conn.Id);

        // 5. Verify SecurityAuditEventType.SecretRotated is recorded with zero secrets
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        var auditEvents = await db.SecurityAuditEvents
            .Where(a => a.TargetId == conn.Id && a.EventType == SecurityAuditEventType.SecretRotated)
            .ToListAsync();
        auditEvents.Should().NotBeEmpty();
        foreach (var ev in auditEvents)
        {
            (ev.MetadataJson ?? string.Empty).Should().NotContain(initialSecret);
            (ev.MetadataJson ?? string.Empty).Should().NotContain(rotatedSecret);
        }
    }

    [Fact]
    public async Task Migration_AddOboTokensAndConnectionGovernance_IsReversibleUpAndDown()
    {
        var dbName = $"WorkflowEngineOboMigTest_{Guid.NewGuid():N}";
        var connStr = $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<WorkflowEngineDbContext>()
            .UseSqlServer(connStr, sql => sql.MigrationsAssembly(typeof(WorkflowEngineDbContext).Assembly.FullName))
            .Options;

        await using var db = new WorkflowEngineDbContext(options);
        try
        {
            var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

            // 1. Migrate up to latest (includes AddOboTokensAndConnectionGovernance)
            await migrator.MigrateAsync();

            var tablesUp = await QueryTableNamesAsync(db);
            tablesUp.Should().Contain("ExternalUserTokens");
            tablesUp.Should().Contain("ConnectionEnvironmentConfigs");
            tablesUp.Should().Contain("ConnectionUsageLogs");

            var connColsUp = await QueryConnectionColumnNamesAsync(db);
            connColsUp.Should().Contain(new[]
            {
                "SecretExpiresAt",
                "LastRotatedAt",
                "HealthStatus",
                "LastTestedAt",
                "LastUsedAt",
                "LastErrorSanitized"
            });

            // 2. Migrate DOWN to previous migration
            await migrator.MigrateAsync("20261127215336_AddSmartObjectSourceLinkAndPrimaryKey");

            var tablesDown = await QueryTableNamesAsync(db);
            tablesDown.Should().NotContain("ExternalUserTokens");
            tablesDown.Should().NotContain("ConnectionEnvironmentConfigs");
            tablesDown.Should().NotContain("ConnectionUsageLogs");

            var connColsDown = await QueryConnectionColumnNamesAsync(db);
            connColsDown.Should().NotContain("HealthStatus");
            connColsDown.Should().NotContain("LastRotatedAt");

            // 3. Migrate back UP to verify full idempotent reversibility
            await migrator.MigrateAsync();

            var tablesReUp = await QueryTableNamesAsync(db);
            tablesReUp.Should().Contain("ExternalUserTokens");
            tablesReUp.Should().Contain("ConnectionEnvironmentConfigs");
            tablesReUp.Should().Contain("ConnectionUsageLogs");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<List<string>> QueryTableNamesAsync(WorkflowEngineDbContext db)
    {
        var list = new List<string>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    private static async Task<List<string>> QueryConnectionColumnNamesAsync(WorkflowEngineDbContext db)
    {
        var list = new List<string>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Connections'";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }
}
