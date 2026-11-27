using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Features.Connections.DTOs;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.SmartForms.DTOs;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

/// <summary>
/// Jalon B / Step B.2 - Versioned SmartObject@Version contracts, SmartObjectNodeExecutor,
/// InternalStore &amp; HttpBinding providers, publish-time contract validation (422),
/// business error routing, non-idempotent technical failure handling, and SmartObjectRecordId linking.
/// </summary>
public class SmartObjectWorkflowActionTests : IClassFixture<HttpConnectorTestWebApplicationFactory>
{
    private readonly HttpConnectorTestWebApplicationFactory _factory;

    public SmartObjectWorkflowActionTests(HttpConnectorTestWebApplicationFactory factory)
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

    [Fact]
    public async Task PublishWorkflow_WhenSmartObjectMethodOrInputInvalid_Returns422UnprocessableEntity()
    {
        var client = await CreateAuthenticatedClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string objectName = $"CustomerContract_{suffix}";

        // 1. Create a Draft SmartObject (not yet Published)
        var createDraftRes = await client.PostAsJsonAsync("/api/smartobjects", new
        {
            name = objectName,
            displayName = "Customer Contract",
            category = "CRM",
            fields = new[]
            {
                new { name = "customerId", type = "String", isPrimaryKey = true, isRequired = true },
                new { name = "creditLimit", type = "Number", isPrimaryKey = false, isRequired = false }
            },
            methods = new object[]
            {
                new
                {
                    name = "GetCustomer",
                    kind = "read",
                    idempotent = true,
                    inputs = new[]
                    {
                        new { name = "customerId", type = "string", required = true },
                        new { name = "minScore", type = "number", required = false }
                    },
                    outputs = new[]
                    {
                        new { name = "name", type = "string", required = false },
                        new { name = "creditLimit", type = "number", required = false }
                    },
                    binding = new { provider = "InternalStore", recordKeyInput = "customerId" }
                }
            }
        });
        createDraftRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var draftSo = await createDraftRes.Content.ReadFromJsonAsync<SmartObjectDto>();
        draftSo.Should().NotBeNull();
        draftSo!.Status.Should().Be("Draft");
        draftSo.Version.Should().Be(1);
        draftSo.ObjectRef.Should().Be($"{objectName}@1");

        // 2. Attempting to publish a workflow referencing the Draft SmartObject -> 422
        string draftGraph = BuildSmartObjectGraph(
            $"wf-draft-{suffix}",
            $"{objectName}@1",
            "GetCustomer",
            """{ "customerId": "CUST-1" }""",
            """{ "custName": "name" }""");

        var draftWfRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = $"wf-draft-{suffix}",
            name = "Draft Ref Workflow",
            graphJson = draftGraph,
            createdBy = "adel.sebti@corp.internal"
        });
        draftWfRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 3. Publish the SmartObject and verify immutability (PUT -> 409 Conflict)
        var pubRes = await client.PostAsync($"/api/smartobjects/{draftSo.Id}/publish", null);
        pubRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var publishedSo = await pubRes.Content.ReadFromJsonAsync<SmartObjectDto>();
        publishedSo!.Status.Should().Be("Published");

        var mutateRes = await client.PutAsJsonAsync($"/api/smartobjects/{draftSo.Id}", new
        {
            displayName = "Attempted Mutation"
        });
        mutateRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 4. Unknown method on Published SmartObject -> 422
        string badMethodGraph = BuildSmartObjectGraph(
            $"wf-badmethod-{suffix}",
            $"{objectName}@1",
            "NonExistentMethod",
            """{ "customerId": "CUST-1" }""",
            """{}""");

        var badMethodRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = $"wf-badmethod-{suffix}",
            name = "Bad Method Workflow",
            graphJson = badMethodGraph,
            createdBy = "adel.sebti@corp.internal"
        });
        badMethodRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 5. Missing required input 'customerId' -> 422
        string missingInputGraph = BuildSmartObjectGraph(
            $"wf-missinginp-{suffix}",
            $"{objectName}@1",
            "GetCustomer",
            """{}""",
            """{}""");

        var missingInputRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = $"wf-missinginp-{suffix}",
            name = "Missing Input Workflow",
            graphJson = missingInputGraph,
            createdBy = "adel.sebti@corp.internal"
        });
        missingInputRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 6. Incompatible literal type ('minScore' expects number, given boolean) -> 422
        string badTypeGraph = BuildSmartObjectGraph(
            $"wf-badtype-{suffix}",
            $"{objectName}@1",
            "GetCustomer",
            """{ "customerId": "CUST-1", "minScore": true }""",
            """{}""");

        var badTypeRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = $"wf-badtype-{suffix}",
            name = "Bad Type Workflow",
            graphJson = badTypeGraph,
            createdBy = "adel.sebti@corp.internal"
        });
        badTypeRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SmartObjectNode_WhenInvoked_ExecutesProviderAndMapsOutputsToInstanceVariables()
    {
        var client = await CreateAuthenticatedClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string objectName = $"Customer_{suffix}";

        // 1. Create & publish versioned SmartObject (InternalStore) with a sensitive output field
        var createRes = await client.PostAsJsonAsync("/api/smartobjects", new
        {
            name = objectName,
            displayName = "Customer Master",
            category = "CRM",
            publishImmediately = true,
            fields = new[]
            {
                new { name = "customerId", type = "String", isPrimaryKey = true, isRequired = true },
                new { name = "name", type = "String", isPrimaryKey = false, isRequired = true },
                new { name = "creditLimit", type = "Number", isPrimaryKey = false, isRequired = false }
            },
            methods = new object[]
            {
                new
                {
                    name = "GetById",
                    kind = "read",
                    idempotent = true,
                    inputs = new[]
                    {
                        new { name = "customerId", type = "string", required = true }
                    },
                    outputs = new object[]
                    {
                        new { name = "name", type = "string" },
                        new { name = "tier", type = "string" },
                        new { name = "creditLimit", type = "number" },
                        new { name = "secretPin", type = "string", isSensitive = true }
                    },
                    binding = new { provider = "InternalStore", recordKeyInput = "customerId" }
                }
            }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var so = await createRes.Content.ReadFromJsonAsync<SmartObjectDto>();
        so!.Status.Should().Be("Published");

        // 2. Seed a record in InternalStore
        var addRecordRes = await client.PostAsJsonAsync($"/api/smartobjects/{so.Id}/records", new Dictionary<string, object?>
        {
            ["customerId"] = "CUST-42",
            ["name"] = "Atlas Logistics",
            ["tier"] = "Enterprise",
            ["creditLimit"] = 75000,
            ["secretPin"] = "TOP-SECRET-PIN-9876"
        });
        addRecordRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Publish valid workflow referencing Customer@1.GetById
        string wfKey = $"wf-so-ok-{suffix}";
        string graphJson = BuildSmartObjectGraph(
            wfKey,
            $"{objectName}@1",
            "GetById",
            """{ "customerId": "{{vars.custId}}" }""",
            """{ "customerName": "name", "customerTier": "tier", "approvedCredit": "creditLimit" }""");

        var defRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "SmartObject Read Workflow",
            graphJson,
            createdBy = "adel.sebti@corp.internal"
        });
        defRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. Start workflow instance
        var startRes = await client.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal",
            triggerVariables = new Dictionary<string, object?> { ["custId"] = "CUST-42" }
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        started!.Status.Should().Be("Completed");

        // 5. Verify outputs were mapped to instance variables
        var inst = await client.GetFromJsonAsync<WorkflowInstanceDto>($"/api/instances/{started.InstanceId}");
        inst!.Status.Should().Be("Completed");
        using var varsDoc = JsonDocument.Parse(inst.ContextVariablesJson!);
        varsDoc.RootElement.GetProperty("customerName").GetString().Should().Be("Atlas Logistics");
        varsDoc.RootElement.GetProperty("customerTier").GetString().Should().Be("Enterprise");
        varsDoc.RootElement.GetProperty("approvedCredit").GetInt32().Should().Be(75000);

        // 6. Verify SmartObject.Invoked audit event exists and masks sensitive values
        var eventsPage = await client.GetFromJsonAsync<InstanceEventsPageDto>($"/api/instances/{started.InstanceId}/events");
        eventsPage.Should().NotBeNull();
        var invokedEvent = eventsPage!.Events.FirstOrDefault(e => e.Type == "SmartObject.Invoked" && e.NodeId == "so-1");
        invokedEvent.Should().NotBeNull();
        invokedEvent!.PayloadJson.Should().Contain("Succeeded");
        invokedEvent.PayloadJson.Should().Contain("***REDACTED***");
        invokedEvent.PayloadJson.Should().NotContain("TOP-SECRET-PIN-9876");

        // 7. Verify SmartFormSubmission links directly to SmartObjectRecordId (no unlinked duplication)
        var createFormRes = await client.PostAsJsonAsync("/api/smartforms", new
        {
            name = $"CustForm_{suffix}",
            title = "Customer Form",
            linkedSmartObjectId = so.Id,
            publishImmediately = true,
            fields = new[]
            {
                new { id = "f1", name = "name", label = "Name", type = "text", required = true }
            }
        });
        createFormRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdForm = await createFormRes.Content.ReadFromJsonAsync<SmartFormDto>();

        using var submitReq = new HttpRequestMessage(HttpMethod.Post, $"/api/smartforms/{createdForm!.Id}/submit")
        {
            Content = JsonContent.Create(new
            {
                data = new Dictionary<string, object?> { ["name"] = "Unified Customer" },
                startLinkedWorkflow = false
            })
        };
        submitReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var submitRes = await client.SendAsync(submitReq);
        submitRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var submission = await submitRes.Content.ReadFromJsonAsync<SmartFormSubmissionDto>();
        submission!.SmartObjectRecordId.Should().NotBeNull();
        submission.SmartObjectRecordId!.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SmartObjectNode_WhenBusinessErrorOccurs_RoutesToErrorBranch()
    {
        var client = await CreateAuthenticatedClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string objectName = $"Account_{suffix}";

        var createRes = await client.PostAsJsonAsync("/api/smartobjects", new
        {
            name = objectName,
            displayName = "Account Store",
            publishImmediately = true,
            fields = new[] { new { name = "accountId", type = "String", isPrimaryKey = true, isRequired = true } },
            methods = new object[]
            {
                new
                {
                    name = "FindAccount",
                    kind = "read",
                    idempotent = true,
                    inputs = new[] { new { name = "accountId", type = "string", required = true } },
                    outputs = new[] { new { name = "owner", type = "string" } },
                    binding = new { provider = "InternalStore", recordKeyInput = "accountId" }
                }
            }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        string wfKey = $"wf-so-bizerr-{suffix}";
        string graphJson = $$$"""
        {
          "id": "{{{wfKey}}}",
          "name": "SmartObject Business Error Branch",
          "version": "1.0",
          "nodes": [
            {
              "id": "trig-1",
              "type": "trigger",
              "position": { "x": 0, "y": 0 },
              "data": { "nodeType": "trigger", "label": "Start" }
            },
            {
              "id": "so-1",
              "type": "smartObject",
              "position": { "x": 200, "y": 0 },
              "data": {
                "nodeType": "smartobject",
                "label": "Lookup Account",
                "smartObjectData": {
                  "smartObjectRef": "{{{objectName}}}@1",
                  "methodName": "FindAccount",
                  "inputMapping": { "accountId": "{{vars.accountId}}" },
                  "outputMapping": { "accountOwner": "owner" },
                  "onBusinessError": {
                    "routeTo": "end-not-found",
                    "errorVariable": "soBizError"
                  }
                }
              }
            },
            {
              "id": "end-ok",
              "type": "end",
              "position": { "x": 450, "y": 0 },
              "data": { "nodeType": "end", "label": "Found", "endData": { "status": "completed", "message": "Found" } }
            },
            {
              "id": "end-not-found",
              "type": "end",
              "position": { "x": 450, "y": 150 },
              "data": { "nodeType": "end", "label": "Account Missing", "endData": { "status": "completed", "message": "Handled NOT_FOUND" } }
            }
          ],
          "edges": [
            { "id": "e1", "source": "trig-1", "target": "so-1" },
            { "id": "e2", "source": "so-1", "target": "end-ok" }
          ]
        }
        """;

        var defRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "SmartObject Business Error Branch",
            graphJson,
            createdBy = "adel.sebti@corp.internal"
        });
        defRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Request a non-existent record -> InternalStore returns BUSINESS / NOT_FOUND
        var startRes = await client.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal",
            triggerVariables = new Dictionary<string, object?> { ["accountId"] = "ACC-DOES-NOT-EXIST" }
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        started!.Status.Should().Be("Completed");
        started.CurrentNodeId.Should().Be("end-not-found");

        var inst = await client.GetFromJsonAsync<WorkflowInstanceDto>($"/api/instances/{started.InstanceId}");
        using var varsDoc = JsonDocument.Parse(inst!.ContextVariablesJson!);
        var errObj = varsDoc.RootElement.GetProperty("soBizError");
        errObj.GetProperty("category").GetString().Should().Be("BUSINESS");
        errObj.GetProperty("code").GetString().Should().Be("NOT_FOUND");

        var eventsPage = await client.GetFromJsonAsync<InstanceEventsPageDto>($"/api/instances/{started.InstanceId}/events");
        eventsPage!.Events.Should().Contain(e =>
            e.Type == "SmartObject.Invoked" &&
            e.NodeId == "so-1" &&
            e.PayloadJson != null &&
            e.PayloadJson.Contains("NOT_FOUND") &&
            e.PayloadJson.Contains("BUSINESS"));
    }

    [Fact]
    public async Task SmartObjectNode_WhenNonIdempotentMethodFailsTechnically_DoesNotAutoRetryAndFaultsInstance()
    {
        var client = await CreateAuthenticatedClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string connName = $"pay-conn-{suffix}";
        string pathPrefix = $"/v1/charge-{suffix}";
        string objectName = $"PaymentGateway_{suffix}";

        // 1. Create Connection (reusing B.1 Connection infrastructure)
        var connRes = await client.PostAsJsonAsync("/api/connections", new
        {
            name = connName,
            baseUrl = "https://api.partner.test",
            authType = "bearer",
            secretRef = "PARTNER_API_TOKEN",
            allowedHosts = new[] { "api.partner.test" }
        });
        connRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Stub HTTP endpoint to always fail with 503 Service Unavailable (TECHNICAL / UNAVAILABLE)
        _factory.Transport.RegisterHandler(pathPrefix, (_, _) =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""{"error":"gateway down"}""", Encoding.UTF8, "application/json")
            });

        // 3. Create & publish SmartObject with non-idempotent HttpBinding method (idempotent = false)
        var soRes = await client.PostAsJsonAsync("/api/smartobjects", new
        {
            name = objectName,
            displayName = "Payment Gateway",
            publishImmediately = true,
            fields = new[] { new { name = "amount", type = "Number", isPrimaryKey = false, isRequired = true } },
            methods = new object[]
            {
                new
                {
                    name = "ChargeCard",
                    kind = "custom",
                    idempotent = false,
                    inputs = new[] { new { name = "amount", type = "number", required = true } },
                    outputs = new[] { new { name = "transactionId", type = "string" } },
                    binding = new
                    {
                        provider = "HttpBinding",
                        connectionRef = connName,
                        httpMethod = "POST",
                        path = pathPrefix
                    }
                }
            }
        });
        soRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. Publish workflow with retryPolicy.maxAttempts = 4 (MUST be ignored because method.idempotent == false!)
        string wfKey = $"wf-so-nonidemp-{suffix}";
        string graphJson = $$$"""
        {
          "id": "{{{wfKey}}}",
          "name": "Non-Idempotent Payment Workflow",
          "version": "1.0",
          "nodes": [
            {
              "id": "trig-1",
              "type": "trigger",
              "position": { "x": 0, "y": 0 },
              "data": { "nodeType": "trigger", "label": "Start" }
            },
            {
              "id": "so-1",
              "type": "smartObject",
              "position": { "x": 200, "y": 0 },
              "data": {
                "nodeType": "smartobject",
                "label": "Charge Card",
                "smartObjectData": {
                  "smartObjectRef": "{{{objectName}}}@1",
                  "methodName": "ChargeCard",
                  "inputMapping": { "amount": 250 },
                  "outputMapping": { "txId": "transactionId" },
                  "retryPolicy": { "maxAttempts": 4, "initialDelayMs": 1, "backoffMultiplier": 1.0, "maxDelayMs": 5 }
                }
              }
            },
            {
              "id": "end-1",
              "type": "end",
              "position": { "x": 450, "y": 0 },
              "data": { "nodeType": "end", "label": "Done", "endData": { "status": "completed", "message": "Charged" } }
            }
          ],
          "edges": [
            { "id": "e1", "source": "trig-1", "target": "so-1" },
            { "id": "e2", "source": "so-1", "target": "end-1" }
          ]
        }
        """;

        var defRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "Non-Idempotent Payment Workflow",
            graphJson,
            createdBy = "adel.sebti@corp.internal"
        });
        defRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 5. Start workflow -> must NOT auto-retry (exactly 1 HTTP call) and must Fault the instance
        var startRes = await client.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal"
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        started!.Status.Should().Be("Faulted");

        _factory.Transport.GetCallCount(pathPrefix).Should().Be(1);

        var eventsPage = await client.GetFromJsonAsync<InstanceEventsPageDto>($"/api/instances/{started.InstanceId}/events");
        var invokedEvent = eventsPage!.Events.FirstOrDefault(e => e.Type == "SmartObject.Invoked" && e.NodeId == "so-1");
        invokedEvent.Should().NotBeNull();
        invokedEvent!.PayloadJson.Should().Contain("TECHNICAL");
        invokedEvent.PayloadJson.Should().Contain("UNAVAILABLE");
        invokedEvent.PayloadJson.Should().Contain("\"attempts\":1");
        invokedEvent.PayloadJson.Should().NotContain(HttpConnectorTestWebApplicationFactory.PartnerBearerSecretValue);
    }

    private static string BuildSmartObjectGraph(
        string wfKey,
        string smartObjectRef,
        string methodName,
        string inputMappingJson,
        string outputMappingJson)
    {
        return $$$"""
        {
          "id": "{{{wfKey}}}",
          "name": "SmartObject Workflow",
          "version": "1.0",
          "nodes": [
            {
              "id": "trig-1",
              "type": "trigger",
              "position": { "x": 0, "y": 0 },
              "data": { "nodeType": "trigger", "label": "Start" }
            },
            {
              "id": "so-1",
              "type": "smartObject",
              "position": { "x": 200, "y": 0 },
              "data": {
                "nodeType": "smartobject",
                "label": "Invoke SmartObject",
                "smartObjectData": {
                  "smartObjectRef": "{{{smartObjectRef}}}",
                  "methodName": "{{{methodName}}}",
                  "inputMapping": {{{inputMappingJson}}},
                  "outputMapping": {{{outputMappingJson}}}
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
            { "id": "e1", "source": "trig-1", "target": "so-1" },
            { "id": "e2", "source": "so-1", "target": "end-1" }
          ]
        }
        """;
    }
}
