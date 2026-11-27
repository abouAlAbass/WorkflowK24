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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WorkflowEngine.Api.Auth;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

public sealed class OidcTestDnsResolver : IDnsResolver
{
    private readonly Dictionary<string, IPAddress[]> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["idp.enterprise.test"] = new[] { IPAddress.Parse("93.184.216.34") },
        ["idp-rotate.enterprise.test"] = new[] { IPAddress.Parse("93.184.216.35") },
        ["private-rebind.test"] = new[] { IPAddress.Parse("10.0.0.42") },
        ["metadata-rebind.test"] = new[] { IPAddress.Parse("169.254.169.254") }
    };

    public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct = default)
    {
        if (_map.TryGetValue(host, out var ips))
            return Task.FromResult(ips);
        return Task.FromResult(Array.Empty<IPAddress>());
    }
}

public sealed class InMemoryLogCapture : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
    public void Dispose() { }

    private sealed class CaptureLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages;
        public CaptureLogger(ConcurrentQueue<string> messages) => _messages = messages;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
            if (exception != null)
            {
                _messages.Enqueue(exception.ToString());
            }
        }
    }
}

public sealed class MockOidcIdpHandler : HttpMessageHandler
{
    private readonly object _keyLock = new();
    private RSA _activeRsa;
    private string _activeKid;

    public int DiscoveryCallCount;
    public int JwksCallCount;
    public int TokenCallCount;

    public string? LastTokenRequestBody { get; private set; }
    public Func<Dictionary<string, string>, string>? IdTokenFactory { get; set; }

    public MockOidcIdpHandler()
    {
        _activeRsa = RSA.Create(2048);
        _activeKid = "key-v1";
    }

    public void RotateSigningKey(string newKid)
    {
        lock (_keyLock)
        {
            _activeRsa.Dispose();
            _activeRsa = RSA.Create(2048);
            _activeKid = newKid;
        }
    }

    public string SignIdToken(
        string issuer,
        string audience,
        string subject,
        string nonce,
        string? email = null,
        bool? emailVerified = null,
        string? name = null,
        IEnumerable<string>? groups = null)
    {
        lock (_keyLock)
        {
            var key = new RsaSecurityKey(_activeRsa.ExportParameters(true)) { KeyId = _activeKid };
            var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subject),
                new(JwtRegisteredClaimNames.Nonce, nonce)
            };

            if (!string.IsNullOrWhiteSpace(email))
                claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
            if (emailVerified.HasValue)
                claims.Add(new Claim("email_verified", emailVerified.Value ? "true" : "false"));
            if (!string.IsNullOrWhiteSpace(name))
                claims.Add(new Claim("name", name));
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    claims.Add(new Claim("groups", g));
                }
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    private string BuildJwksJson()
    {
        lock (_keyLock)
        {
            var pubParams = _activeRsa.ExportParameters(false);
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(pubParams) { KeyId = _activeKid });
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;

            var payload = new
            {
                keys = new[]
                {
                    new
                    {
                        kty = jwk.Kty,
                        use = jwk.Use,
                        kid = jwk.Kid,
                        alg = jwk.Alg,
                        n = jwk.N,
                        e = jwk.E
                    }
                }
            };

            return JsonSerializer.Serialize(payload);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        string authority = $"{uri.Scheme}://{uri.Authority}";

        if (uri.AbsolutePath.EndsWith("/.well-known/openid-configuration", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref DiscoveryCallCount);
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
            Interlocked.Increment(ref JwksCallCount);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildJwksJson(), Encoding.UTF8, "application/json")
            };
        }

        if (uri.AbsolutePath.EndsWith("/token", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
        {
            Interlocked.Increment(ref TokenCallCount);
            string body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            LastTokenRequestBody = body;

            var parsed = HttpUtility.ParseQueryString(body);
            var form = parsed.AllKeys
                .Where(k => k != null)
                .ToDictionary(k => k!, k => parsed[k!] ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            string idToken = IdTokenFactory?.Invoke(form)
                ?? throw new InvalidOperationException("IdTokenFactory not configured for test.");

            var tokenJson = JsonSerializer.Serialize(new
            {
                access_token = "mock-access-token",
                token_type = "Bearer",
                expires_in = 3600,
                id_token = idToken
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
            };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

public class ExternalOidcTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _testDbName = $"WorkflowEngineOidcTestDb_{Guid.NewGuid():N}";

    public MockOidcIdpHandler MockIdp { get; } = new();
    public OidcTestDnsResolver DnsResolver { get; } = new();
    public InMemoryLogCapture LogCapture { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "SqlServer");
        builder.UseSetting(
            "Database:ConnectionString",
            $"Server=(localdb)\\mssqllocaldb;Database={_testDbName};Trusted_Connection=True;MultipleActiveResultSets=true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "500");
        builder.UseSetting("Seed:PrimaryAdmin:Email", "adel.sebti@corp.internal");
        builder.UseSetting("Auth:FrontendBaseUrl", "http://localhost:5173");

        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(LogCapture);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDnsResolver>();
            services.AddSingleton<IDnsResolver>(DnsResolver);

            services.AddHttpClient(OidcHttpClientNames.OidcProvider)
                .ConfigurePrimaryHttpMessageHandler(() => MockIdp);
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

public class ExternalOidcAuthTests : IClassFixture<ExternalOidcTestWebApplicationFactory>
{
    private readonly ExternalOidcTestWebApplicationFactory _factory;

    public ExternalOidcAuthTests(ExternalOidcTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "adel.sebti@corp.internal",
        string password = DatabaseInitializer.DefaultSeedPassword)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private HttpClient CreateAnonymousClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static (string State, string Nonce, string CodeChallenge) ParseChallengeRedirect(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location;
        location.Should().NotBeNull();
        var qs = HttpUtility.ParseQueryString(location!.Query);
        qs["response_type"].Should().Be("code");
        qs["code_challenge_method"].Should().Be("S256");

        string state = qs["state"]!;
        string nonce = qs["nonce"]!;
        string codeChallenge = qs["code_challenge"]!;
        state.Should().NotBeNullOrWhiteSpace();
        nonce.Should().NotBeNullOrWhiteSpace();
        codeChallenge.Should().NotBeNullOrWhiteSpace();
        return (state, nonce, codeChallenge);
    }

    [Fact]
    public async Task ExternalChallengeAndCallback_IssuesOneTimeExchangeCode_NeverLeakingJwtInRedirectUrl()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var anon = CreateAnonymousClient();

        const string authority = "https://idp.enterprise.test";
        const string clientId = "tamam-client-01";
        const string clientSecret = "oidc-secret-alpha-001";

        var createRes = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: $"Entra ID {Guid.NewGuid():N}",
            Protocol: "oidc",
            Authority: authority,
            ClientId: clientId,
            ClientSecret: clientSecret,
            Scopes: "openid profile email",
            RoleMappingJson: "{\"corp-designers\":\"Designer\"}",
            JitProvisioningEnabled: true,
            AllowEmailMatching: false,
            AllowAdminRoleMapping: false,
            DefaultRole: "User",
            IsEnabled: true));

        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var provider = await createRes.Content.ReadFromJsonAsync<AuthProviderDto>();
        provider.Should().NotBeNull();
        provider!.HasSecret.Should().BeTrue();

        // Public discovery endpoint lists enabled provider + local break-glass login
        var publicProvidersRes = await anon.GetAsync("/api/auth/providers");
        publicProvidersRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicProviders = await publicProvidersRes.Content.ReadFromJsonAsync<PublicAuthProvidersResponse>();
        publicProviders!.LocalLoginEnabled.Should().BeTrue();
        publicProviders.Providers.Should().Contain(p => p.Id == provider.Id);

        // 1. Challenge -> 302 with PKCE S256 + state + nonce
        var challengeRes = await anon.GetAsync($"/api/auth/external/{provider.Id}/challenge");
        var (state, nonce, codeChallenge) = ParseChallengeRedirect(challengeRes);

        string expectedSub = $"sub-jit-{Guid.NewGuid():N}";
        string expectedEmail = $"jit.{Guid.NewGuid():N}@enterprise.test";

        _factory.MockIdp.IdTokenFactory = form =>
        {
            // Verify PKCE code_verifier matches S256 code_challenge and client_secret was decrypted
            form["client_secret"].Should().Be(clientSecret);
            OidcFlowStateStore.ComputeS256CodeChallenge(form["code_verifier"]).Should().Be(codeChallenge);
            return _factory.MockIdp.SignIdToken(
                issuer: authority,
                audience: clientId,
                subject: expectedSub,
                nonce: nonce,
                email: expectedEmail,
                emailVerified: true,
                name: "Nadia SSO",
                groups: new[] { "corp-designers" });
        };

        // 2. Callback -> 302 to /login/callback?exchangeCode=... (NEVER JWT in URL)
        var callbackRes = await anon.GetAsync($"/api/auth/external/{provider.Id}/callback?code=auth-code-123&state={Uri.EscapeDataString(state)}");
        callbackRes.StatusCode.Should().Be(HttpStatusCode.Redirect);
        string redirectUrl = callbackRes.Headers.Location!.ToString();
        redirectUrl.Should().Contain("/login/callback?exchangeCode=");
        redirectUrl.Should().NotContain("eyJ");
        redirectUrl.Should().NotContain("token=");

        var redirectQuery = HttpUtility.ParseQueryString(callbackRes.Headers.Location.Query);
        string exchangeCode = redirectQuery["exchangeCode"]!;
        exchangeCode.Should().NotBeNullOrWhiteSpace();

        // State cannot be replayed
        var replayStateRes = await anon.GetAsync($"/api/auth/external/{provider.Id}/callback?code=auth-code-123&state={Uri.EscapeDataString(state)}");
        replayStateRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 3. Exchange opaque code -> Tamam JWT LoginResponse
        var exchangeRes = await anon.PostAsJsonAsync("/api/auth/external/exchange", new ExternalExchangeRequest(exchangeCode));
        exchangeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await exchangeRes.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.Token.Should().NotBeNullOrWhiteSpace();
        login.Email.Should().Be(expectedEmail);
        login.Role.Should().Be("Designer");

        // 4. Exchange code is strictly single-use
        var replayExchangeRes = await anon.PostAsJsonAsync("/api/auth/external/exchange", new ExternalExchangeRequest(exchangeCode));
        replayExchangeRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 5. Issued Tamam JWT works on authenticated endpoint
        var ssoClient = CreateAnonymousClient();
        ssoClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var meRes = await ssoClient.GetAsync("/api/auth/me");
        meRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExternalCallback_LinksUserByProviderAndSub_AndPreventsUnverifiedEmailAccountTakeover()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var anon = CreateAnonymousClient();

        const string authority = "https://idp.enterprise.test";
        const string clientId = "tamam-client-02";

        var createRes = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: $"Okta Corp {Guid.NewGuid():N}",
            Protocol: "oidc",
            Authority: authority,
            ClientId: clientId,
            AllowEmailMatching: false,
            JitProvisioningEnabled: true,
            IsEnabled: true));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var provider = (await createRes.Content.ReadFromJsonAsync<AuthProviderDto>())!;

        // 1. Unverified email matching an existing local account MUST be blocked (prevents account takeover)
        var ch1 = ParseChallengeRedirect(await anon.GetAsync($"/api/auth/external/{provider.Id}/challenge"));
        _factory.MockIdp.IdTokenFactory = _ => _factory.MockIdp.SignIdToken(
            issuer: authority,
            audience: clientId,
            subject: "attacker-sub-999",
            nonce: ch1.Nonce,
            email: "karim.khmissi@corp.internal",
            emailVerified: false,
            name: "Attacker");

        var blockedRes = await anon.GetAsync($"/api/auth/external/{provider.Id}/callback?code=c1&state={Uri.EscapeDataString(ch1.State)}");
        blockedRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var blockedBody = await blockedRes.Content.ReadAsStringAsync();
        blockedBody.Should().Contain("UNVERIFIED_EMAIL_MATCH_BLOCKED");

        // 2. Verified email links cleanly to existing local user and creates ExternalUserLogin(provider, sub)
        string legitSub = $"karim-sub-{Guid.NewGuid():N}";
        var ch2 = ParseChallengeRedirect(await anon.GetAsync($"/api/auth/external/{provider.Id}/challenge"));
        _factory.MockIdp.IdTokenFactory = _ => _factory.MockIdp.SignIdToken(
            issuer: authority,
            audience: clientId,
            subject: legitSub,
            nonce: ch2.Nonce,
            email: "karim.khmissi@corp.internal",
            emailVerified: true,
            name: "Karim Khmissi");

        var okRes = await anon.GetAsync($"/api/auth/external/{provider.Id}/callback?code=c2&state={Uri.EscapeDataString(ch2.State)}");
        okRes.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var code2 = HttpUtility.ParseQueryString(okRes.Headers.Location!.Query)["exchangeCode"]!;
        var login2 = await (await anon.PostAsJsonAsync("/api/auth/external/exchange", new ExternalExchangeRequest(code2)))
            .Content.ReadFromJsonAsync<LoginResponse>();
        login2!.UserId.Should().Be("usr-6");

        // 3. Subsequent login resolves by (ProviderId, ExternalSubject) even if IdP email changes
        var ch3 = ParseChallengeRedirect(await anon.GetAsync($"/api/auth/external/{provider.Id}/challenge"));
        _factory.MockIdp.IdTokenFactory = _ => _factory.MockIdp.SignIdToken(
            issuer: authority,
            audience: clientId,
            subject: legitSub,
            nonce: ch3.Nonce,
            email: "karim.renamed@corp.internal",
            emailVerified: false,
            name: "Karim Khmissi");

        var okRes3 = await anon.GetAsync($"/api/auth/external/{provider.Id}/callback?code=c3&state={Uri.EscapeDataString(ch3.State)}");
        okRes3.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var code3 = HttpUtility.ParseQueryString(okRes3.Headers.Location!.Query)["exchangeCode"]!;
        var login3 = await (await anon.PostAsJsonAsync("/api/auth/external/exchange", new ExternalExchangeRequest(code3)))
            .Content.ReadFromJsonAsync<LoginResponse>();
        login3!.UserId.Should().Be("usr-6");
    }

    [Fact]
    public async Task ExternalCallback_WhenJitDisabledAndUnknownUser_Returns403UserNotProvisioned()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var anon = CreateAnonymousClient();

        const string authority = "https://idp.enterprise.test";
        const string clientId = "tamam-client-03";

        var createRes = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: $"Strict No-JIT {Guid.NewGuid():N}",
            Protocol: "oidc",
            Authority: authority,
            ClientId: clientId,
            JitProvisioningEnabled: false,
            AllowEmailMatching: false,
            IsEnabled: true));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var provider = (await createRes.Content.ReadFromJsonAsync<AuthProviderDto>())!;

        var challenge = ParseChallengeRedirect(await anon.GetAsync($"/api/auth/external/{provider.Id}/challenge"));
        string unknownEmail = $"unknown.{Guid.NewGuid():N}@external.test";
        _factory.MockIdp.IdTokenFactory = _ => _factory.MockIdp.SignIdToken(
            issuer: authority,
            audience: clientId,
            subject: $"unknown-sub-{Guid.NewGuid():N}",
            nonce: challenge.Nonce,
            email: unknownEmail,
            emailVerified: true,
            name: "Unknown External");

        var callbackRes = await anon.GetAsync($"/api/auth/external/{provider.Id}/callback?code=c-nojit&state={Uri.EscapeDataString(challenge.State)}");
        callbackRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await callbackRes.Content.ReadAsStringAsync();
        body.Should().Contain("USER_NOT_PROVISIONED");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        (await db.SystemUsers.AnyAsync(u => u.Email == unknownEmail)).Should().BeFalse();
    }

    [Fact]
    public async Task AdminAuthProviders_BlocksAdminRoleMappingWithoutExplicitFlag_AndEnforcesSsrfGuard()
    {
        var nonAdmin = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");
        var admin = await CreateAuthenticatedClientAsync();

        // Non-admin receives 403 Forbidden
        var forbiddenGet = await nonAdmin.GetAsync("/api/admin/auth-providers");
        forbiddenGet.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var forbiddenPost = await nonAdmin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: "Unauthorized IdP",
            Protocol: "oidc",
            Authority: "https://idp.enterprise.test",
            ClientId: "cid"));
        forbiddenPost.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Mapping to Admin without AllowAdminRoleMapping == true -> 400 ADMIN_MAPPING_CONFIRMATION_REQUIRED
        var blockedAdminMap = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: $"Blocked Admin Map {Guid.NewGuid():N}",
            Protocol: "oidc",
            Authority: "https://idp.enterprise.test",
            ClientId: "cid-admin",
            RoleMappingJson: "{\"grp-super\":\"Admin\"}",
            AllowAdminRoleMapping: false));
        blockedAdminMap.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blockedAdminMap.Content.ReadAsStringAsync()).Should().Contain("ADMIN_MAPPING_CONFIRMATION_REQUIRED");

        // DefaultRole = "Admin" is always forbidden
        var blockedDefaultAdmin = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: $"Blocked Default Admin {Guid.NewGuid():N}",
            Protocol: "oidc",
            Authority: "https://idp.enterprise.test",
            ClientId: "cid-default",
            DefaultRole: "Admin"));
        blockedDefaultAdmin.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // SSRF guard blocks loopback, link-local metadata, and private RFC1918 DNS rebinds
        foreach (var badAuthority in new[]
        {
            "https://127.0.0.1/oidc",
            "https://169.254.169.254/latest",
            "https://private-rebind.test/oidc",
            "https://metadata-rebind.test/oidc"
        })
        {
            var ssrfRes = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
                Name: $"SSRF {Guid.NewGuid():N}",
                Protocol: "oidc",
                Authority: badAuthority,
                ClientId: "cid-ssrf"));
            ssrfRes.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"authority {badAuthority} must be blocked by SSRF guard");
            (await ssrfRes.Content.ReadAsStringAsync()).Should().Contain("SSRF_BLOCKED");
        }

        // Valid creation with explicit AllowAdminRoleMapping == true succeeds, hides secret, and records SecurityAuditEvent
        const string secretValue = "admin-provider-secret-do-not-expose-777";
        var okRes = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: $"Explicit Admin IdP {Guid.NewGuid():N}",
            Protocol: "oidc",
            Authority: "https://idp.enterprise.test",
            ClientId: "cid-explicit-admin",
            ClientSecret: secretValue,
            RoleMappingJson: "{\"grp-super\":\"Admin\"}",
            AllowAdminRoleMapping: true));
        okRes.StatusCode.Should().Be(HttpStatusCode.Created);
        string rawJson = await okRes.Content.ReadAsStringAsync();
        rawJson.Should().NotContain(secretValue);
        rawJson.Should().NotContain("encryptedClientSecret");
        var created = JsonSerializer.Deserialize<AuthProviderDto>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        created.HasSecret.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        var audit = await db.SecurityAuditEvents
            .Where(e => e.EventType == SecurityAuditEventType.AuthProviderChanged && e.TargetId == created.Id)
            .FirstOrDefaultAsync();
        audit.Should().NotBeNull();
        (audit!.MetadataJson ?? string.Empty).Should().NotContain(secretValue);
    }

    [Fact]
    public async Task OidcJwksCache_RefreshesAutomaticallyOnKeyRotation_AndLogsZeroSecrets()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var anon = CreateAnonymousClient();

        const string authority = "https://idp-rotate.enterprise.test";
        const string clientId = "tamam-rotate-client";
        const string secretSentinel = "super-sensitive-oidc-secret-ROTATE-999111";

        var createRes = await admin.PostAsJsonAsync("/api/admin/auth-providers", new UpsertAuthProviderRequest(
            Name: $"Key Rotation IdP {Guid.NewGuid():N}",
            Protocol: "oidc",
            Authority: authority,
            ClientId: clientId,
            ClientSecret: secretSentinel,
            JitProvisioningEnabled: true,
            IsEnabled: true));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var provider = (await createRes.Content.ReadFromJsonAsync<AuthProviderDto>())!;

        int jwksCallsBefore = Volatile.Read(ref _factory.MockIdp.JwksCallCount);

        // Login #1 with initial RSA key (key-v1)
        var ch1 = ParseChallengeRedirect(await anon.GetAsync($"/api/auth/external/{provider.Id}/challenge"));
        _factory.MockIdp.IdTokenFactory = _ => _factory.MockIdp.SignIdToken(
            issuer: authority,
            audience: clientId,
            subject: $"rot-sub-1-{Guid.NewGuid():N}",
            nonce: ch1.Nonce,
            email: $"rot1.{Guid.NewGuid():N}@enterprise.test",
            emailVerified: true,
            name: "Rotation User 1");

        var cb1 = await anon.GetAsync($"/api/auth/external/{provider.Id}/callback?code=c-rot-1&state={Uri.EscapeDataString(ch1.State)}");
        cb1.StatusCode.Should().Be(HttpStatusCode.Redirect);
        int jwksCallsAfterFirst = Volatile.Read(ref _factory.MockIdp.JwksCallCount);
        jwksCallsAfterFirst.Should().Be(jwksCallsBefore + 1);

        // Rotate IdP signing key to key-v2 while key-v1 is still cached in IOidcDiscoveryCache
        _factory.MockIdp.RotateSigningKey("key-v2");

        // Login #2 signed with key-v2 -> cache misses kid, automatically refreshes JWKS once, and succeeds
        var ch2 = ParseChallengeRedirect(await anon.GetAsync($"/api/auth/external/{provider.Id}/challenge"));
        _factory.MockIdp.IdTokenFactory = _ => _factory.MockIdp.SignIdToken(
            issuer: authority,
            audience: clientId,
            subject: $"rot-sub-2-{Guid.NewGuid():N}",
            nonce: ch2.Nonce,
            email: $"rot2.{Guid.NewGuid():N}@enterprise.test",
            emailVerified: true,
            name: "Rotation User 2");

        var cb2 = await anon.GetAsync($"/api/auth/external/{provider.Id}/callback?code=c-rot-2&state={Uri.EscapeDataString(ch2.State)}");
        cb2.StatusCode.Should().Be(HttpStatusCode.Redirect);
        int jwksCallsAfterRotation = Volatile.Read(ref _factory.MockIdp.JwksCallCount);
        jwksCallsAfterRotation.Should().Be(jwksCallsAfterFirst + 1);

        // Exchange code from post-rotation login works cleanly
        var code2 = HttpUtility.ParseQueryString(cb2.Headers.Location!.Query)["exchangeCode"]!;
        var ex2 = await anon.PostAsJsonAsync("/api/auth/external/exchange", new ExternalExchangeRequest(code2));
        ex2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify ZERO secret leakage across all captured logs
        var allLogs = string.Join("\n", _factory.LogCapture.Messages);
        allLogs.Should().NotContain(secretSentinel);
    }
}
