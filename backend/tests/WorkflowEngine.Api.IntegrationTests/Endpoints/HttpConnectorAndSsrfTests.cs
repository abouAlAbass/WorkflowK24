using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Features.Connections.DTOs;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

public sealed class StubDnsResolver : IDnsResolver
{
    private readonly ConcurrentDictionary<string, IPAddress[]> _map = new(StringComparer.OrdinalIgnoreCase);

    public StubDnsResolver()
    {
        // Safe public test hosts (RFC 5737 / example.com public IP)
        _map["api.partner.test"] = new[] { IPAddress.Parse("93.184.216.34") };
        _map["erp.partner.test"] = new[] { IPAddress.Parse("93.184.216.35") };

        // Simulated DNS rebinding / private internal hosts
        _map["private-rebind.test"] = new[] { IPAddress.Parse("10.0.0.42") };
        _map["metadata-rebind.test"] = new[] { IPAddress.Parse("169.254.169.254") };
        _map["loopback-rebind.test"] = new[] { IPAddress.Parse("127.0.0.1") };
    }

    public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct = default)
    {
        if (_map.TryGetValue(host, out var ips))
        {
            return Task.FromResult(ips);
        }

        return Task.FromResult(Array.Empty<IPAddress>());
    }
}

public sealed record RecordedHttpCall(
    string Method,
    string Uri,
    Dictionary<string, string> Headers,
    string? Body
);

public sealed class RecordingHttpTransport : IHttpConnectorTransport
{
    private readonly ConcurrentDictionary<string, Func<HttpRequestMessage, int, HttpResponseMessage>> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _callCounts = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentQueue<RecordedHttpCall> Calls { get; } = new();

    public void RegisterHandler(string pathPrefix, Func<HttpRequestMessage, int, HttpResponseMessage> handler)
    {
        _handlers[pathPrefix] = handler;
        _callCounts[pathPrefix] = 0;
    }

    public int GetCallCount(string pathPrefix) => _callCounts.TryGetValue(pathPrefix, out int count) ? count : 0;

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken ct = default)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in request.Headers)
        {
            headers[h.Key] = string.Join(", ", h.Value);
        }

        string? body = request.Content != null ? await request.Content.ReadAsStringAsync(ct) : null;
        string absoluteUri = request.RequestUri?.ToString() ?? string.Empty;
        Calls.Enqueue(new RecordedHttpCall(request.Method.Method, absoluteUri, headers, body));

        string path = request.RequestUri?.AbsolutePath ?? "/";
        foreach (var kv in _handlers)
        {
            if (path.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                int attempt = _callCounts.AddOrUpdate(kv.Key, 1, (_, c) => c + 1);
                return kv.Value(request, attempt);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"error\":\"no test stub registered\"}", Encoding.UTF8, "application/json")
        };
    }
}

public class HttpConnectorTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _testDbName = $"WorkflowEngineHttpTestDb_{Guid.NewGuid():N}";

    public const string PartnerBearerSecretValue = "super-secret-bearer-token-99999";
    public RecordingHttpTransport Transport { get; } = new();
    public StubDnsResolver DnsResolver { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "SqlServer");
        builder.UseSetting(
            "Database:ConnectionString",
            $"Server=(localdb)\\mssqllocaldb;Database={_testDbName};Trusted_Connection=True;MultipleActiveResultSets=true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "500");
        builder.UseSetting("Seed:PrimaryAdmin:Email", "adel.sebti@corp.internal");
        builder.UseSetting("Secrets:PARTNER_API_TOKEN", PartnerBearerSecretValue);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHttpConnectorTransport>();
            services.AddSingleton<IHttpConnectorTransport>(Transport);

            services.RemoveAll<IDnsResolver>();
            services.AddSingleton<IDnsResolver>(DnsResolver);
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

/// <summary>
/// Jalon B / Step B.1 - Connection entity, RBAC, HttpNodeExecutor, SSRF guard, retry classification &amp; secret redaction.
/// </summary>
public class HttpConnectorAndSsrfTests : IClassFixture<HttpConnectorTestWebApplicationFactory>
{
    private readonly HttpConnectorTestWebApplicationFactory _factory;

    public HttpConnectorAndSsrfTests(HttpConnectorTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "adel.sebti@corp.internal",
        string password = DatabaseInitializer.DefaultSeedPassword)
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private async Task<ConnectionDto> CreateConnectionAsync(
        HttpClient client,
        string name,
        string baseUrl,
        string authType,
        string? secretRef,
        string[] allowedHosts)
    {
        var res = await client.PostAsJsonAsync("/api/connections", new
        {
            name,
            baseUrl,
            authType,
            secretRef,
            allowedHosts
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await res.Content.ReadFromJsonAsync<ConnectionDto>();
        dto.Should().NotBeNull();
        return dto!;
    }

    [Fact]
    public async Task HttpNode_WhenSuccess200_MapsResponseVariablesAndCompletesInstance()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string connName = $"partner-ok-{suffix}";
        string pathPrefix = $"/v1/orders-ok-{suffix}";

        await CreateConnectionAsync(
            adminClient,
            connName,
            "https://api.partner.test",
            "bearer",
            "PARTNER_API_TOKEN",
            new[] { "api.partner.test" });

        // Verify GET /api/connections never exposes resolved secrets
        var listText = await adminClient.GetStringAsync("/api/connections");
        listText.Should().Contain(connName);
        listText.Should().NotContain(HttpConnectorTestWebApplicationFactory.PartnerBearerSecretValue);

        // Verify End User receives 403 Forbidden on /api/connections
        string endUserEmail = $"enduser-{suffix}@corp.internal";
        var createUserRes = await adminClient.PostAsJsonAsync("/api/users", new
        {
            name = "Standard End User",
            email = endUserEmail,
            role = "End User",
            group = "General",
            status = "Active",
            password = DatabaseInitializer.DefaultSeedPassword
        });
        createUserRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var endUserClient = await CreateAuthenticatedClientAsync(endUserEmail);
        var forbiddenRes = await endUserClient.GetAsync("/api/connections");
        forbiddenRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _factory.Transport.RegisterHandler(pathPrefix, (_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":{"id":"ERP-ORDER-42","amount":1250.5,"confirmed":true}}""",
                    Encoding.UTF8,
                    "application/json")
            });

        string wfKey = $"wf-http-ok-{suffix}";
        string graphJson = $$$"""
        {
          "id": "{{{wfKey}}}",
          "name": "HTTP Success Workflow",
          "version": "1.0",
          "nodes": [
            {
              "id": "trig-1",
              "type": "trigger",
              "position": { "x": 0, "y": 0 },
              "data": { "nodeType": "trigger", "label": "Start" }
            },
            {
              "id": "http-1",
              "type": "http",
              "position": { "x": 200, "y": 0 },
              "data": {
                "nodeType": "http",
                "label": "Create ERP Order",
                "httpData": {
                  "connectionRef": "{{{connName}}}",
                  "method": "POST",
                  "path": "{{{pathPrefix}}}/{{vars.orderId}}",
                  "body": {
                    "customer": "{{vars.customer}}",
                    "execKey": "{{execution_key}}"
                  },
                  "responseMapping": {
                    "erpOrderId": "$.data.id",
                    "approvedAmount": "$.data.amount",
                    "isConfirmed": "$.data.confirmed"
                  }
                }
              }
            },
            {
              "id": "end-1",
              "type": "end",
              "position": { "x": 400, "y": 0 },
              "data": { "nodeType": "end", "label": "Done", "endData": { "status": "completed", "message": "OK" } }
            }
          ],
          "edges": [
            { "id": "e1", "source": "trig-1", "target": "http-1" },
            { "id": "e2", "source": "http-1", "target": "end-1" }
          ]
        }
        """;

        var defRes = await adminClient.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "HTTP Success Workflow",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Integrations"
        });
        defRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var startRes = await adminClient.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal",
            triggerVariables = new Dictionary<string, object?>
            {
                ["orderId"] = "ORD-100",
                ["customer"] = "Acme Corp"
            }
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        started.Should().NotBeNull();
        started!.Status.Should().Be("Completed");

        // Verify HTTP call details (template resolution + Idempotency-Key)
        var call = _factory.Transport.Calls.Last(c => c.Uri.Contains(pathPrefix, StringComparison.OrdinalIgnoreCase));
        call.Method.Should().Be("POST");
        call.Uri.Should().EndWith($"{pathPrefix}/ORD-100");
        call.Headers.Should().ContainKey("Idempotency-Key");
        call.Headers["Idempotency-Key"].Should().Be($"{started.InstanceId}:http-1");
        call.Body.Should().Contain("Acme Corp");
        call.Body.Should().Contain($"{started.InstanceId}:http-1");

        // Verify mapped typed variables persisted on instance
        var inst = await adminClient.GetFromJsonAsync<WorkflowInstanceDto>($"/api/instances/{started.InstanceId}");
        inst.Should().NotBeNull();
        inst!.Status.Should().Be("Completed");
        using var varsDoc = JsonDocument.Parse(inst.ContextVariablesJson!);
        varsDoc.RootElement.GetProperty("erpOrderId").GetString().Should().Be("ERP-ORDER-42");
        varsDoc.RootElement.GetProperty("approvedAmount").GetDouble().Should().Be(1250.5);
        varsDoc.RootElement.GetProperty("isConfirmed").GetBoolean().Should().BeTrue();

        // Verify InstanceEvents contain Http.AttemptStarted and Http.Succeeded
        var eventsPage = await adminClient.GetFromJsonAsync<InstanceEventsPageDto>($"/api/instances/{started.InstanceId}/events");
        eventsPage.Should().NotBeNull();
        eventsPage!.Events.Should().Contain(e => e.Type == "Http.AttemptStarted" && e.NodeId == "http-1");
        eventsPage.Events.Should().Contain(e => e.Type == "Http.Succeeded" && e.NodeId == "http-1");
    }

    [Fact]
    public async Task HttpNode_When5xxThen200_RetriesWithBackoffAndSucceeds()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string connName = $"partner-retry-{suffix}";
        string pathPrefix = $"/v1/retry-{suffix}";

        await CreateConnectionAsync(
            adminClient,
            connName,
            "https://api.partner.test",
            "none",
            null,
            new[] { "api.partner.test" });

        _factory.Transport.RegisterHandler(pathPrefix, (_, attempt) =>
        {
            if (attempt == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"error\":\"temporary outage\"}", Encoding.UTF8, "application/json")
                };
            }

            if (attempt == 2)
            {
                var rateLimited = new HttpResponseMessage((HttpStatusCode)429)
                {
                    Content = new StringContent("{\"error\":\"slow down\"}", Encoding.UTF8, "application/json")
                };
                rateLimited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));
                return rateLimited;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"result":"recovered"}""", Encoding.UTF8, "application/json")
            };
        });

        string wfKey = $"wf-http-retry-{suffix}";
        string graphJson = $$"""
        {
          "id": "{{wfKey}}",
          "name": "HTTP Retry Workflow",
          "version": "1.0",
          "nodes": [
            { "id": "trig-1", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            {
              "id": "http-retry",
              "type": "http",
              "position": { "x": 200, "y": 0 },
              "data": {
                "nodeType": "http",
                "label": "Flaky Partner API",
                "httpData": {
                  "connectionRef": "{{connName}}",
                  "method": "GET",
                  "path": "{{pathPrefix}}",
                  "retryPolicy": { "maxAttempts": 3, "initialDelayMs": 10, "backoffMultiplier": 2.0, "maxDelayMs": 50 },
                  "responseMapping": { "apiResult": "$.result" }
                }
              }
            },
            { "id": "end-1", "type": "end", "position": { "x": 400, "y": 0 }, "data": { "nodeType": "end", "label": "Done", "endData": { "status": "completed", "message": "OK" } } }
          ],
          "edges": [
            { "id": "e1", "source": "trig-1", "target": "http-retry" },
            { "id": "e2", "source": "http-retry", "target": "end-1" }
          ]
        }
        """;

        (await adminClient.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "HTTP Retry Workflow",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Integrations"
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var startRes = await adminClient.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal"
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        started!.Status.Should().Be("Completed");

        _factory.Transport.GetCallCount(pathPrefix).Should().Be(3);

        var eventsPage = await adminClient.GetFromJsonAsync<InstanceEventsPageDto>($"/api/instances/{started.InstanceId}/events");
        eventsPage!.Events.Count(e => e.Type == "Http.AttemptStarted").Should().Be(3);
        eventsPage.Events.Count(e => e.Type == "Http.AttemptFailed").Should().Be(2);
        eventsPage.Events.Where(e => e.Type == "Http.AttemptFailed").Should().OnlyContain(e =>
            e.PayloadJson!.Contains("\"classification\":\"TECHNICAL\"") &&
            e.PayloadJson!.Contains("\"willRetry\":true"));
        eventsPage.Events.Should().ContainSingle(e => e.Type == "Http.Succeeded");

        var inst = await adminClient.GetFromJsonAsync<WorkflowInstanceDto>($"/api/instances/{started.InstanceId}");
        using var varsDoc = JsonDocument.Parse(inst!.ContextVariablesJson!);
        varsDoc.RootElement.GetProperty("apiResult").GetString().Should().Be("recovered");
    }

    [Fact]
    public async Task HttpNode_When4xxBusinessError_RoutesToOnBusinessErrorNode()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string connName = $"partner-biz-{suffix}";
        string pathPrefix = $"/v1/biz-{suffix}";

        await CreateConnectionAsync(
            adminClient,
            connName,
            "https://api.partner.test",
            "none",
            null,
            new[] { "api.partner.test" });

        _factory.Transport.RegisterHandler(pathPrefix, (_, _) =>
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("""{"code":"INSUFFICIENT_CREDIT","message":"Limit exceeded"}""", Encoding.UTF8, "application/json")
            });

        string wfKey = $"wf-http-biz-{suffix}";
        string graphJson = $$"""
        {
          "id": "{{wfKey}}",
          "name": "HTTP Business Error Routing Workflow",
          "version": "1.0",
          "nodes": [
            { "id": "trig-1", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            {
              "id": "http-biz",
              "type": "http",
              "position": { "x": 200, "y": 0 },
              "data": {
                "nodeType": "http",
                "label": "Credit Check API",
                "httpData": {
                  "connectionRef": "{{connName}}",
                  "method": "POST",
                  "path": "{{pathPrefix}}",
                  "retryPolicy": { "maxAttempts": 3, "initialDelayMs": 10, "backoffMultiplier": 2.0, "maxDelayMs": 50 },
                  "onBusinessError": {
                    "routeTo": "end-business-error",
                    "errorVariable": "creditError"
                  }
                }
              }
            },
            { "id": "end-normal", "type": "end", "position": { "x": 450, "y": -80 }, "data": { "nodeType": "end", "label": "Normal End", "endData": { "status": "completed", "message": "Normal" } } },
            { "id": "end-business-error", "type": "end", "position": { "x": 450, "y": 100 }, "data": { "nodeType": "end", "label": "Business Error Handled", "endData": { "status": "rejected", "message": "Handled 4xx" } } }
          ],
          "edges": [
            { "id": "e1", "source": "trig-1", "target": "http-biz" },
            { "id": "e2", "source": "http-biz", "target": "end-normal" }
          ]
        }
        """;

        (await adminClient.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "HTTP Business Error Routing Workflow",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Integrations"
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var startRes = await adminClient.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal"
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();

        // Must NOT retry on 4xx business error
        _factory.Transport.GetCallCount(pathPrefix).Should().Be(1);

        // Routed to business error terminal node instead of Faulted or end-normal
        started!.Status.Should().Be("Completed");
        started.CurrentNodeId.Should().Be("end-business-error");

        var inst = await adminClient.GetFromJsonAsync<WorkflowInstanceDto>($"/api/instances/{started.InstanceId}");
        using var varsDoc = JsonDocument.Parse(inst!.ContextVariablesJson!);
        varsDoc.RootElement.GetProperty("creditError").GetProperty("statusCode").GetInt32().Should().Be(422);

        var eventsPage = await adminClient.GetFromJsonAsync<InstanceEventsPageDto>($"/api/instances/{started.InstanceId}/events");
        eventsPage!.Events.Should().ContainSingle(e =>
            e.Type == "Http.AttemptFailed" &&
            e.PayloadJson!.Contains("\"classification\":\"BUSINESS\"") &&
            e.PayloadJson!.Contains("\"willRetry\":false"));
    }

    [Fact]
    public async Task HttpNode_WhenTargetIsPrivateIpOrCloudMetadata_BlockedBySsrfGuardWithoutRetry()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string suffix = Guid.NewGuid().ToString("N")[..8];

        // Case A: Direct AWS/Azure cloud metadata IP (169.254.169.254) even if present in allowedHosts
        string metaConn = $"conn-meta-{suffix}";
        await CreateConnectionAsync(
            adminClient,
            metaConn,
            "http://169.254.169.254",
            "none",
            null,
            new[] { "169.254.169.254" });

        // Case B: DNS rebinding to RFC1918 private IP (10.0.0.42)
        string rebindConn = $"conn-rebind-{suffix}";
        await CreateConnectionAsync(
            adminClient,
            rebindConn,
            "https://private-rebind.test",
            "none",
            null,
            new[] { "private-rebind.test" });

        // Case C: Allowed public host redirecting to cloud metadata IP
        string redirConn = $"conn-redir-{suffix}";
        string redirPath = $"/v1/redir-{suffix}";
        await CreateConnectionAsync(
            adminClient,
            redirConn,
            "https://api.partner.test",
            "none",
            null,
            new[] { "api.partner.test" });

        _factory.Transport.RegisterHandler(redirPath, (_, _) =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.Found);
            resp.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data/");
            return resp;
        });

        async Task<(WorkflowInstanceStartedResult Started, InstanceEventsPageDto Events)> RunHttpWorkflowAsync(string wfKey, string connectionRef, string path)
        {
            string graphJson = $$"""
            {
              "id": "{{wfKey}}",
              "name": "SSRF Test Workflow",
              "version": "1.0",
              "nodes": [
                { "id": "trig-1", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
                {
                  "id": "http-ssrf",
                  "type": "http",
                  "position": { "x": 200, "y": 0 },
                  "data": {
                    "nodeType": "http",
                    "label": "SSRF Target",
                    "httpData": {
                      "connectionRef": "{{connectionRef}}",
                      "method": "GET",
                      "path": "{{path}}",
                      "retryPolicy": { "maxAttempts": 3, "initialDelayMs": 10, "backoffMultiplier": 2.0, "maxDelayMs": 50 }
                    }
                  }
                },
                { "id": "end-1", "type": "end", "position": { "x": 400, "y": 0 }, "data": { "nodeType": "end", "label": "Done", "endData": { "status": "completed", "message": "OK" } } }
              ],
              "edges": [
                { "id": "e1", "source": "trig-1", "target": "http-ssrf" },
                { "id": "e2", "source": "http-ssrf", "target": "end-1" }
              ]
            }
            """;

            (await adminClient.PostAsJsonAsync("/api/definitions", new
            {
                workflowKey = wfKey,
                name = "SSRF Test Workflow",
                graphJson,
                createdBy = "adel.sebti@corp.internal",
                category = "Security"
            })).StatusCode.Should().Be(HttpStatusCode.Created);

            var startRes = await adminClient.PostAsJsonAsync("/api/instances/start", new
            {
                workflowKey = wfKey,
                initiatorId = "adel.sebti@corp.internal"
            });
            startRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var started = (await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>())!;
            var events = (await adminClient.GetFromJsonAsync<InstanceEventsPageDto>($"/api/instances/{started.InstanceId}/events"))!;
            return (started, events);
        }

        int callsBeforeDirect = _factory.Transport.Calls.Count;

        // Run Case A (169.254.169.254)
        var (metaStarted, metaEvents) = await RunHttpWorkflowAsync($"wf-ssrf-meta-{suffix}", metaConn, "/latest/meta-data/");
        metaStarted.Status.Should().Be("Faulted");
        metaEvents.Events.Should().ContainSingle(e =>
            e.Type == "Http.AttemptFailed" &&
            e.PayloadJson!.Contains("\"classification\":\"FATAL\"") &&
            e.PayloadJson!.Contains("SSRF_BLOCKED"));

        // Run Case B (private IP via DNS)
        var (rebindStarted, rebindEvents) = await RunHttpWorkflowAsync($"wf-ssrf-rebind-{suffix}", rebindConn, "/internal");
        rebindStarted.Status.Should().Be("Faulted");
        rebindEvents.Events.Should().ContainSingle(e =>
            e.Type == "Http.AttemptFailed" &&
            e.PayloadJson!.Contains("\"classification\":\"FATAL\"") &&
            e.PayloadJson!.Contains("SSRF_BLOCKED"));

        // Zero network calls should have been made for Case A and Case B!
        _factory.Transport.Calls.Count.Should().Be(callsBeforeDirect);

        // Run Case C (redirect to 169.254.169.254)
        var (redirStarted, redirEvents) = await RunHttpWorkflowAsync($"wf-ssrf-redir-{suffix}", redirConn, redirPath);
        redirStarted.Status.Should().Be("Faulted");
        _factory.Transport.GetCallCount(redirPath).Should().Be(1); // Only initial hop, no retry and never follows redirect
        _factory.Transport.Calls.Should().NotContain(c => c.Uri.Contains("169.254.169.254"));
        redirEvents.Events.Should().ContainSingle(e =>
            e.Type == "Http.AttemptFailed" &&
            e.PayloadJson!.Contains("\"classification\":\"FATAL\"") &&
            e.PayloadJson!.Contains("SSRF_REDIRECT_BLOCKED"));
    }

    [Fact]
    public async Task HttpNode_NeverLogsAuthorizationHeaderOrSecretsInInstanceEvents()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string connName = $"partner-secret-{suffix}";
        string pathPrefix = $"/v1/secret-check-{suffix}";

        await CreateConnectionAsync(
            adminClient,
            connName,
            "https://api.partner.test",
            "bearer",
            "PARTNER_API_TOKEN",
            new[] { "api.partner.test" });

        _factory.Transport.RegisterHandler(pathPrefix, (_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true}""", Encoding.UTF8, "application/json")
            });

        string wfKey = $"wf-http-secret-{suffix}";
        string graphJson = $$"""
        {
          "id": "{{wfKey}}",
          "name": "HTTP Secret Redaction Workflow",
          "version": "1.0",
          "nodes": [
            { "id": "trig-1", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            {
              "id": "http-sec",
              "type": "http",
              "position": { "x": 200, "y": 0 },
              "data": {
                "nodeType": "http",
                "label": "Authenticated Partner Call",
                "httpData": {
                  "connectionRef": "{{connName}}",
                  "method": "GET",
                  "path": "{{pathPrefix}}"
                }
              }
            },
            { "id": "end-1", "type": "end", "position": { "x": 400, "y": 0 }, "data": { "nodeType": "end", "label": "Done", "endData": { "status": "completed", "message": "OK" } } }
          ],
          "edges": [
            { "id": "e1", "source": "trig-1", "target": "http-sec" },
            { "id": "e2", "source": "http-sec", "target": "end-1" }
          ]
        }
        """;

        (await adminClient.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "HTTP Secret Redaction Workflow",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Security"
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var startRes = await adminClient.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal"
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = (await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>())!;
        started.Status.Should().Be("Completed");

        // Confirm Authorization header WAS actually sent on the wire
        var call = _factory.Transport.Calls.Last(c => c.Uri.Contains(pathPrefix, StringComparison.OrdinalIgnoreCase));
        call.Headers.Should().ContainKey("Authorization");
        call.Headers["Authorization"].Should().Be($"Bearer {HttpConnectorTestWebApplicationFactory.PartnerBearerSecretValue}");

        // Confirm API events response contains ZERO occurrences of Authorization or the secret token
        string eventsRawJson = await adminClient.GetStringAsync($"/api/instances/{started.InstanceId}/events");
        eventsRawJson.Should().Contain("Http.AttemptStarted");
        eventsRawJson.Should().Contain("Http.Succeeded");
        eventsRawJson.Should().NotContain(HttpConnectorTestWebApplicationFactory.PartnerBearerSecretValue);
        eventsRawJson.Should().NotContain("Authorization", "Authorization header must never be logged in InstanceEvents");

        // Confirm directly in database tables (InstanceEvents, WorkflowAuditLogs, WorkflowInstanceErrors)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();

        var dbEvents = await db.InstanceEvents.Where(e => e.InstanceId == started.InstanceId).ToListAsync();
        dbEvents.Should().NotBeEmpty();
        foreach (var ev in dbEvents)
        {
            (ev.PayloadJson ?? string.Empty).Should().NotContain(HttpConnectorTestWebApplicationFactory.PartnerBearerSecretValue);
            (ev.PayloadJson ?? string.Empty).Should().NotContain("Authorization");
        }

        var dbAuditLogs = await db.WorkflowAuditLogs.Where(a => a.InstanceId == started.InstanceId).ToListAsync();
        foreach (var log in dbAuditLogs)
        {
            (log.EventSummary ?? string.Empty).Should().NotContain(HttpConnectorTestWebApplicationFactory.PartnerBearerSecretValue);
            (log.StateSnapshotJson ?? string.Empty).Should().NotContain(HttpConnectorTestWebApplicationFactory.PartnerBearerSecretValue);
        }
    }
}
