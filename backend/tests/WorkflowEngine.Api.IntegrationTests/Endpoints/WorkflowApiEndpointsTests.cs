using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _testDbName = $"WorkflowEngineTestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "SqlServer");
        builder.UseSetting(
            "Database:ConnectionString",
            $"Server=(localdb)\\mssqllocaldb;Database={_testDbName};Trusted_Connection=True;MultipleActiveResultSets=true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "500");

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
            // Ignore cleanup exceptions if host was never initialized
        }

        await base.DisposeAsync();
    }
}

public class WorkflowApiEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public WorkflowApiEndpointsTests(TestWebApplicationFactory factory)
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
    public async Task GetHealth_ReturnsOkWithEngineInfo()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
        content.Should().Contain("WorkflowEngine .NET 10");
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutBearerToken_ReturnUnauthorized()
    {
        var anonymousClient = _factory.CreateClient();

        (await anonymousClient.GetAsync("/api/definitions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymousClient.GetAsync("/api/instances")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymousClient.GetAsync("/api/smartobjects")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymousClient.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymousClient.GetAsync("/api/settings")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymousClient.GetAsync("/api/analytics/kpis")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostLogin_WithValidCredentials_ReturnsJwtToken()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("adel.sebti@corp.internal", "Password123!");

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.Email.Should().Be("adel.sebti@corp.internal");
        body.Name.Should().Be("Adel Sebti");
    }

    [Fact]
    public async Task PostLogin_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("adel.sebti@corp.internal", "WrongPassword999!");

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostLogin_WithInactiveUser_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("y.benali@enterprise.internal", "Password123!");

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostLogin_WithEmptyEmail_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("", "");

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminOnlyEndpoints_WithNonAdminUser_ReturnForbidden()
    {
        var managerClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        var settingsRes = await managerClient.GetAsync("/api/settings");
        settingsRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createUserRes = await managerClient.PostAsJsonAsync("/api/users", new
        {
            name = "Unauthorized Create",
            email = "unauth@enterprise.internal",
            role = "End User",
            group = "General"
        });
        createUserRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSmartObjects_ReturnsOkWithList()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/smartobjects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostSmartObject_WithValidPayload_ReturnsCreated()
    {
        var client = await CreateAuthenticatedClientAsync();
        var newEntity = new
        {
            id = "so-test-api",
            name = "TestEntityApi",
            displayName = "Test Entity API",
            description = "Created in test",
            category = "Custom",
            fields = new[]
            {
                new { name = "Id", type = "Number", isPrimaryKey = true, isRequired = true },
                new { name = "Title", type = "String", isPrimaryKey = false, isRequired = true }
            }
        };

        var response = await client.PostAsJsonAsync("/api/smartobjects", newEntity);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetUsers_ReturnsOkWithList()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("a.sebti@enterprise.internal");
    }

    [Fact]
    public async Task PostUser_WithValidPayload_ReturnsCreated()
    {
        var client = await CreateAuthenticatedClientAsync();
        var newUser = new
        {
            name = "Test Engineer",
            email = $"test.eng.{Guid.NewGuid():N}@enterprise.internal",
            role = "Process Manager",
            group = "Operations",
            status = "Active",
            password = "SecurePassword123!"
        };

        var response = await client.PostAsJsonAsync("/api/users", newUser);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetAnalyticsKpis_ReturnsOkWithAggregates()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/analytics/kpis");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("avgCompletionTime");
        content.Should().Contain("slaComplianceRate");
    }

    [Fact]
    public async Task GetSettings_ReturnsOkWithConfig()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("smtpHost");
    }

    [Fact]
    public async Task PutSettings_WithValidPayload_ReturnsOkWithUpdatedConfig()
    {
        var client = await CreateAuthenticatedClientAsync();
        var update = new
        {
            smtpHost = "smtp.updated.domain.internal",
            smtpPort = "2525",
            sqlMaxPoolSize = 150
        };

        var response = await client.PutAsJsonAsync("/api/settings", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("smtp.updated.domain.internal");
    }

    [Fact]
    public async Task DeleteUser_ExistingUser_ReturnsNoContent()
    {
        var client = await CreateAuthenticatedClientAsync();
        var newUser = new
        {
            name = "Temp User To Delete",
            email = $"delete.me.{Guid.NewGuid():N}@enterprise.internal",
            role = "End User",
            group = "Operations",
            status = "Active"
        };
        var createRes = await client.PostAsJsonAsync("/api/users", newUser);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<WorkflowEngine.Application.Features.Users.DTOs.UserDto>();

        var deleteRes = await client.DeleteAsync($"/api/users/{created!.Id}");

        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ApiResponses_IncludeHardenedSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("Content-Security-Policy").First().Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task CreateAndDeleteDefinition_ValidatesGraphAndSupportsDeletionAndDynamicTaskOutcomes()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Reject invalid GraphJson with duplicate node IDs or script injection
        var invalidDef = new
        {
            workflowKey = "bad-wf",
            name = "<script>alert(1)</script>",
            graphJson = "{\"nodes\":[{\"id\":\"dup\"},{\"id\":\"dup\"}]}",
            createdBy = "adel.sebti@corp.internal"
        };
        var invalidRes = await client.PostAsJsonAsync("/api/definitions", invalidDef);
        invalidRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2. Create valid workflow definition with custom dynamic outcomes
        string wfKey = $"po-dynamic-{Guid.NewGuid():N}"[..24];
        string graphJson = """
        {
          "id": "po-wf",
          "name": "Dynamic PO Workflow",
          "version": "1.0",
          "nodes": [
            {
              "id": "trig-1",
              "type": "trigger",
              "position": { "x": 0, "y": 0 },
              "data": { "nodeType": "trigger", "label": "Start" }
            },
            {
              "id": "appr-custom",
              "type": "approval",
              "position": { "x": 200, "y": 0 },
              "data": {
                "nodeType": "approval",
                "label": "CFO Custom Decision",
                "approvalData": {
                  "assignmentType": "user",
                  "assignee": "karim.khmissi@corp.internal",
                  "outcomes": [
                    { "id": "fast-track", "label": "Fast-Track Sign-Off", "color": "#10B981" },
                    { "id": "escalate", "label": "Escalate to Board", "color": "#F59E0B" }
                  ],
                  "dueDateDays": 3
                }
              }
            }
          ],
          "edges": [
            { "id": "e1", "source": "trig-1", "target": "appr-custom" }
          ]
        }
        """;

        var createRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "Dynamic PO Workflow",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Finance"
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Start instance and verify dynamic outcomes are returned by GET /api/tasks/assignee/{email}
        var startRes = await client.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal",
            triggerVariables = new { department = "Treasury", amount = 25000 }
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var tasksJson = await client.GetStringAsync("/api/tasks/assignee/karim.khmissi%40corp.internal");
        tasksJson.Should().Contain("Fast-Track Sign-Off");
        tasksJson.Should().Contain("Escalate to Board");
        tasksJson.Should().Contain("Dynamic PO Workflow");

        // 4. Delete definition via DELETE /api/definitions/{key}
        var delDefRes = await client.DeleteAsync($"/api/definitions/{wfKey}");
        delDefRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfterDel = await client.GetAsync($"/api/definitions/{wfKey}");
        getAfterDel.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SmartFormsAndAttachments_GenerateFromSmartObject_SubmitAndScanAttachments()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Create a SmartObject to generate a SmartForm from
        string soId = $"so-vendor-{Guid.NewGuid():N}"[..18];
        var createSoRes = await client.PostAsJsonAsync("/api/smartobjects", new
        {
            id = soId,
            name = "VendorContract",
            displayName = "Vendor Contract",
            description = "Legal vendor contracts",
            category = "Finance",
            fields = new[]
            {
                new { name = "ContractId", type = "GUID", isPrimaryKey = true, isRequired = true },
                new { name = "SupplierName", type = "String", isPrimaryKey = false, isRequired = true },
                new { name = "ContractAmount", type = "Number", isPrimaryKey = false, isRequired = true }
            }
        });
        createSoRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. 1-Click Generate SmartForm from SmartObject
        var genRes = await client.PostAsync($"/api/smartforms/generate/{soId}", null);
        genRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var genJson = await genRes.Content.ReadAsStringAsync();
        genJson.Should().Contain("Vendor Contract Form");
        genJson.Should().Contain("SupplierName");
        genJson.Should().Contain("currency");

        string generatedFormId = $"form-{soId}";

        // 3. Submit SmartForm and verify bound SmartObject record is automatically stored
        var submitRes = await client.PostAsJsonAsync($"/api/smartforms/{generatedFormId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["SupplierName"] = "Capgemini Enterprise",
                ["ContractAmount"] = 48000
            },
            startLinkedWorkflow = false
        });
        submitRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var recordsJson = await client.GetStringAsync($"/api/smartobjects/{soId}/records");
        recordsJson.Should().Contain("Capgemini Enterprise");

        // 4. Reject dangerous executable attachment (.exe) and EICAR signature
        var blockedExeRes = await client.PostAsJsonAsync("/api/attachments/upload", new
        {
            fileName = "payload.exe",
            contentType = "application/octet-stream",
            base64Content = Convert.ToBase64String(new byte[] { 0x4D, 0x5A, 0x90, 0x00 })
        });
        blockedExeRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var eicarBytes = System.Text.Encoding.ASCII.GetBytes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        var blockedMalwareRes = await client.PostAsJsonAsync("/api/attachments/upload", new
        {
            fileName = "invoice.pdf",
            contentType = "application/pdf",
            base64Content = Convert.ToBase64String(eicarBytes)
        });
        blockedMalwareRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 5. Upload clean PDF attachment and stream it back
        var pdfBytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 Clean Vendor Quote Document");
        var cleanUploadRes = await client.PostAsJsonAsync("/api/attachments/upload", new
        {
            fileName = "vendor-quote.pdf",
            contentType = "application/pdf",
            base64Content = Convert.ToBase64String(pdfBytes)
        });
        cleanUploadRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploadedDto = await cleanUploadRes.Content.ReadFromJsonAsync<WorkflowEngine.Application.Features.SmartForms.DTOs.FormAttachmentDto>();
        uploadedDto.Should().NotBeNull();
        uploadedDto!.Sha256Hash.Should().NotBeNullOrWhiteSpace();

        var downloadRes = await client.GetAsync($"/api/attachments/{uploadedDto.Id}");
        downloadRes.StatusCode.Should().Be(HttpStatusCode.OK);
        (await downloadRes.Content.ReadAsByteArrayAsync()).Should().Equal(pdfBytes);
    }

    private async Task<(string InstanceId, string TaskId)> CreateAndStartApprovalWorkflowAsync(
        HttpClient designerClient,
        string assignee = "karim.khmissi@corp.internal",
        string assignmentType = "user",
        string? formId = "form-purchase-req",
        string formMode = "readonly",
        object? triggerVariables = null)
    {
        string wfKey = $"wf-task-{Guid.NewGuid():N}"[..24];
        string formIdJson = formId != null ? $"\"{formId}\"" : "null";
        string graphJson = $$"""
        {
          "id": "{{wfKey}}",
          "name": "Approval Task Contract Workflow",
          "version": "1.0",
          "nodes": [
            {
              "id": "trig-start",
              "type": "trigger",
              "position": { "x": 0, "y": 0 },
              "data": { "nodeType": "trigger", "label": "Start" }
            },
            {
              "id": "appr-node-1",
              "type": "approval",
              "position": { "x": 220, "y": 0 },
              "data": {
                "nodeType": "approval",
                "label": "Manager Review Task",
                "approvalData": {
                  "assignmentType": "{{assignmentType}}",
                  "assignee": "{{assignee}}",
                  "outcomes": [
                    { "id": "approve", "label": "Approve", "color": "#059669" },
                    { "id": "reject", "label": "Reject", "color": "#DC2626" }
                  ],
                  "dueDateDays": 2,
                  "formId": {{formIdJson}},
                  "formMode": "{{formMode}}"
                }
              }
            },
            {
              "id": "end-1",
              "type": "end",
              "position": { "x": 450, "y": 0 },
              "data": {
                "nodeType": "end",
                "label": "Completed",
                "endData": { "status": "completed", "message": "Done" }
              }
            }
          ],
          "edges": [
            { "id": "e1", "source": "trig-start", "target": "appr-node-1" },
            { "id": "e2", "source": "appr-node-1", "target": "end-1", "sourceHandle": "approve" }
          ]
        }
        """;

        var createDefRes = await designerClient.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "Approval Task Contract Workflow",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Finance"
        });
        createDefRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var startRes = await designerClient.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal",
            triggerVariables = triggerVariables ?? new Dictionary<string, object?>
            {
                ["VendorName"] = "Dell Technologies",
                ["Department"] = "Finance",
                ["Amount"] = 4500
            }
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowEngine.Application.Features.Instances.DTOs.WorkflowInstanceStartedResult>();
        started.Should().NotBeNull();

        var tasks = await designerClient.GetFromJsonAsync<List<WorkflowEngine.Application.Features.Tasks.DTOs.WorkflowTaskDto>>(
            $"/api/tasks/instance/{started!.InstanceId}");
        tasks.Should().NotBeNull();
        tasks.Should().ContainSingle();

        return (started.InstanceId, tasks![0].TaskId);
    }

    [Fact]
    public async Task CompleteTask_WhenCallerIsNotAssigneeNorAdmin_Returns403Forbidden()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var (_, taskId) = await CreateAndStartApprovalWorkflowAsync(
            adminClient,
            assignee: "karim.khmissi@corp.internal",
            assignmentType: "user",
            formMode: "readonly");

        // Authenticate as m.fetni@enterprise.internal (Process Manager in Finance — neither assignee nor Admin)
        var unauthorizedUserClient = await CreateAuthenticatedClientAsync("m.fetni@enterprise.internal");

        var completeRes = await unauthorizedUserClient.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "approve",
            comments = "Unauthorized approval attempt"
        });

        completeRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Verify the task remains Pending and untouched
        var taskAfter = await adminClient.GetFromJsonAsync<WorkflowEngine.Application.Features.Tasks.DTOs.WorkflowTaskDto>(
            $"/api/tasks/{taskId}");
        taskAfter.Should().NotBeNull();
        taskAfter!.Status.Should().Be("Pending");
        taskAfter.CompletedBy.Should().BeNull();
    }

    [Fact]
    public async Task CompleteTask_WhenTaskAlreadyCompleted_Returns409Conflict()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var (_, taskId) = await CreateAndStartApprovalWorkflowAsync(
            adminClient,
            assignee: "karim.khmissi@corp.internal",
            assignmentType: "user",
            formMode: "readonly");

        var assigneeClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        var firstRes = await assigneeClient.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "approve",
            comments = "First legitimate approval"
        });
        firstRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicateRes = await assigneeClient.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "reject",
            comments = "Second conflicting completion attempt"
        });
        duplicateRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CompleteTask_WhenFormModeIsReadOnlyAndPatchProvided_Returns400BadRequest()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var (_, taskId) = await CreateAndStartApprovalWorkflowAsync(
            adminClient,
            assignee: "karim.khmissi@corp.internal",
            assignmentType: "user",
            formId: "form-purchase-req",
            formMode: "readonly",
            triggerVariables: new Dictionary<string, object?>
            {
                ["VendorName"] = "Dell Technologies",
                ["Amount"] = 4500
            });

        var assigneeClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        var response = await assigneeClient.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "approve",
            comments = "Attempting to mutate read-only form",
            formPatch = new Dictionary<string, object?>
            {
                ["Amount"] = 99999
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorPayload = await response.Content.ReadAsStringAsync();
        errorPayload.Should().Contain("FORM_READ_ONLY");

        var taskAfter = await assigneeClient.GetFromJsonAsync<WorkflowEngine.Application.Features.Tasks.DTOs.WorkflowTaskDto>(
            $"/api/tasks/{taskId}");
        taskAfter!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task CompleteTask_WhenFormModeIsEditable_MergesFormPatchIntoInstanceVariablesAndWritesAuditLog()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string editableFormId = $"form-edit-{Guid.NewGuid():N}"[..20];
        var createFormRes = await adminClient.PostAsJsonAsync("/api/smartforms", new
        {
            id = editableFormId,
            name = "EditablePurchaseReviewForm",
            title = "Editable Purchase Review Form",
            category = "Finance",
            fields = new object[]
            {
                new { id = "f1", name = "VendorName", label = "Vendor Name", type = "text", required = true, readOnly = false },
                new { id = "f2", name = "Department", label = "Department", type = "text", required = true, readOnly = false },
                new { id = "f3", name = "Amount", label = "Amount", type = "currency", required = true, readOnly = false },
                new { id = "f4", name = "ApproverNotes", label = "Approver Notes", type = "textarea", required = false, readOnly = false }
            }
        });
        createFormRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var (instanceId, taskId) = await CreateAndStartApprovalWorkflowAsync(
            adminClient,
            assignee: "karim.khmissi@corp.internal",
            assignmentType: "user",
            formId: editableFormId,
            formMode: "editable",
            triggerVariables: new Dictionary<string, object?>
            {
                ["VendorName"] = "Dell Technologies",
                ["Department"] = "Finance",
                ["Amount"] = 4500
            });

        var assigneeClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        var completeRes = await assigneeClient.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "approve",
            comments = "Approved with negotiated discount",
            formPatch = new Dictionary<string, object?>
            {
                ["Amount"] = 4100,
                ["ApproverNotes"] = "Negotiated enterprise discount"
            }
        });

        completeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 1. Verify task CompletedBy comes strictly from JWT identity
        var completedTask = await adminClient.GetFromJsonAsync<WorkflowEngine.Application.Features.Tasks.DTOs.WorkflowTaskDto>(
            $"/api/tasks/{taskId}");
        completedTask.Should().NotBeNull();
        completedTask!.CompletedBy.Should().Be("karim.khmissi@corp.internal");
        completedTask.Status.Should().Be("Approved");

        // 2. Verify FormPatch was merged into WorkflowInstance.ContextVariablesJson while preserving untouched keys
        var updatedInstance = await adminClient.GetFromJsonAsync<WorkflowEngine.Application.Features.Instances.DTOs.WorkflowInstanceDto>(
            $"/api/instances/{instanceId}");
        updatedInstance.Should().NotBeNull();
        updatedInstance!.ContextVariablesJson.Should().NotBeNullOrWhiteSpace();

        using var varsDoc = System.Text.Json.JsonDocument.Parse(updatedInstance.ContextVariablesJson!);
        varsDoc.RootElement.GetProperty("VendorName").GetString().Should().Be("Dell Technologies");
        varsDoc.RootElement.GetProperty("Department").GetString().Should().Be("Finance");
        varsDoc.RootElement.GetProperty("Amount").GetInt64().Should().Be(4100);
        varsDoc.RootElement.GetProperty("ApproverNotes").GetString().Should().Be("Negotiated enterprise discount");

        // 3. Verify WorkflowAuditLog recorded JWT ActorUserId, outcome, and Before / After JSON diff
        var timeline = await adminClient.GetFromJsonAsync<List<WorkflowEngine.Application.Features.Diagnostics.DTOs.AuditLogDto>>(
            $"/api/diagnostics/timeline/{instanceId}");
        timeline.Should().NotBeNull();

        var patchAudit = timeline!.FirstOrDefault(l => l.EventType == "VariableUpdated" && l.NodeId == "appr-node-1");
        patchAudit.Should().NotBeNull();
        patchAudit!.ActorUserId.Should().Be("karim.khmissi@corp.internal");
        patchAudit.OutcomeBranch.Should().Be("approve");
        patchAudit.BeforeJson.Should().NotBeNullOrWhiteSpace();
        patchAudit.AfterJson.Should().NotBeNullOrWhiteSpace();

        using var beforeDoc = System.Text.Json.JsonDocument.Parse(patchAudit.BeforeJson!);
        beforeDoc.RootElement.GetProperty("Amount").GetInt64().Should().Be(4500);
        beforeDoc.RootElement.GetProperty("ApproverNotes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        using var afterDoc = System.Text.Json.JsonDocument.Parse(patchAudit.AfterJson!);
        afterDoc.RootElement.GetProperty("Amount").GetInt64().Should().Be(4100);
        afterDoc.RootElement.GetProperty("ApproverNotes").GetString().Should().Be("Negotiated enterprise discount");
    }

    [Fact]
    public async Task GetTasks_ReturnsFormIdFormModeAndRealInstanceVariables()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var (instanceId, taskId) = await CreateAndStartApprovalWorkflowAsync(
            adminClient,
            assignee: "karim.khmissi@corp.internal",
            assignmentType: "user",
            formId: "form-purchase-req",
            formMode: "editable",
            triggerVariables: new Dictionary<string, object?>
            {
                ["VendorName"] = "Capgemini Cloud Services",
                ["Department"] = "IT Security",
                ["Amount"] = 18750
            });

        var assigneeClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        var tasksRes = await assigneeClient.GetAsync("/api/tasks?status=Pending");
        tasksRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var tasksJson = await tasksRes.Content.ReadAsStringAsync();
        using var listDoc = System.Text.Json.JsonDocument.Parse(tasksJson);
        var matchingTask = listDoc.RootElement.EnumerateArray()
            .FirstOrDefault(el =>
                el.GetProperty("taskId").GetString() == taskId &&
                el.GetProperty("instanceId").GetString() == instanceId);

        matchingTask.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        matchingTask.GetProperty("formId").GetString().Should().Be("form-purchase-req");
        matchingTask.GetProperty("formMode").GetString().Should().Be("editable");

        var formData = matchingTask.GetProperty("formData");
        formData.GetProperty("VendorName").GetString().Should().Be("Capgemini Cloud Services");
        formData.GetProperty("Department").GetString().Should().Be("IT Security");
        formData.GetProperty("Amount").GetInt64().Should().Be(18750);
    }

    [Fact]
    public async Task GetAttachment_WhenCallerIsNotOwnerNorAdmin_Returns403Forbidden()
    {
        // 1. Owner (karim.khmissi@corp.internal) uploads a valid PDF attachment
        var ownerClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");
        var pdfBytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 Confidential Finance Quote");

        var uploadRes = await ownerClient.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "confidential-quote.pdf",
            contentType = "application/pdf",
            base64Content = Convert.ToBase64String(pdfBytes)
        });
        uploadRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = await uploadRes.Content.ReadFromJsonAsync<WorkflowEngine.Application.Features.SmartForms.DTOs.FormAttachmentDto>();
        uploaded.Should().NotBeNull();
        uploaded!.UploadedByUserId.Should().Be("karim.khmissi@corp.internal");

        // 2. Unrelated non-admin user (m.fetni@enterprise.internal) tries to GET the attachment -> 403 Forbidden
        var otherUserClient = await CreateAuthenticatedClientAsync("m.fetni@enterprise.internal");
        var forbiddenRes = await otherUserClient.GetAsync($"/api/attachments/{uploaded.Id}");
        forbiddenRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Verify authorized workflow participant (task assignee on linked WorkflowInstanceId) CAN read it,
        //    while an unrelated non-participant still gets 403 Forbidden
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var (instanceId, _) = await CreateAndStartApprovalWorkflowAsync(
            adminClient,
            assignee: "m.fetni@enterprise.internal",
            assignmentType: "user");

        var boundUploadRes = await ownerClient.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "instance-quote.pdf",
            contentType = "application/pdf",
            base64Content = Convert.ToBase64String(pdfBytes),
            workflowInstanceId = instanceId
        });
        boundUploadRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var boundDto = await boundUploadRes.Content.ReadFromJsonAsync<WorkflowEngine.Application.Features.SmartForms.DTOs.FormAttachmentDto>();

        var participantRes = await otherUserClient.GetAsync($"/api/attachments/{boundDto!.Id}");
        participantRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var nonParticipantClient = await CreateAuthenticatedClientAsync("k.khmissi@enterprise.internal");
        var nonParticipantRes = await nonParticipantClient.GetAsync($"/api/attachments/{boundDto.Id}");
        nonParticipantRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAttachment_WhenCallerIsNotOwnerNorAdmin_Returns403Forbidden()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var (instanceId, _) = await CreateAndStartApprovalWorkflowAsync(
            adminClient,
            assignee: "m.fetni@enterprise.internal",
            assignmentType: "user");

        var ownerClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");
        var pdfBytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 Protected Vendor Contract");

        var uploadRes = await ownerClient.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "protected-contract.pdf",
            contentType = "application/pdf",
            base64Content = Convert.ToBase64String(pdfBytes),
            workflowInstanceId = instanceId
        });
        uploadRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = await uploadRes.Content.ReadFromJsonAsync<WorkflowEngine.Application.Features.SmartForms.DTOs.FormAttachmentDto>();

        // Even a workflow task participant who is neither Owner nor Admin cannot DELETE the attachment
        var nonOwnerClient = await CreateAuthenticatedClientAsync("m.fetni@enterprise.internal");
        var deleteRes = await nonOwnerClient.DeleteAsync($"/api/attachments/{uploaded!.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Owner CAN delete their attachment
        var ownerDeleteRes = await ownerClient.DeleteAsync($"/api/attachments/{uploaded.Id}");
        ownerDeleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UploadAttachment_WhenHtmlOrSvgDisguisedAsPdf_Returns400BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        // 1. HTML script payload disguised with .pdf extension and application/pdf Content-Type
        var htmlBytes = System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body><script>alert('XSS')</script></body></html>");
        var disguisedHtmlRes = await client.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "fake-invoice.pdf",
            contentType = "application/pdf",
            base64Content = Convert.ToBase64String(htmlBytes)
        });
        disguisedHtmlRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await disguisedHtmlRes.Content.ReadAsStringAsync()).Should().Contain("INVALID_FILE_TYPE");

        // 2. SVG XSS payload disguised as .pdf
        var svgBytes = System.Text.Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"></svg>");
        var disguisedSvgRes = await client.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "diagram.pdf",
            contentType = "application/pdf",
            base64Content = Convert.ToBase64String(svgBytes)
        });
        disguisedSvgRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await disguisedSvgRes.Content.ReadAsStringAsync()).Should().Contain("INVALID_FILE_TYPE");

        // 3. Explicit .svg and .html extensions are also rejected
        var directSvgRes = await client.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "logo.svg",
            contentType = "image/svg+xml",
            base64Content = Convert.ToBase64String(svgBytes)
        });
        directSvgRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await directSvgRes.Content.ReadAsStringAsync()).Should().Contain("INVALID_FILE_TYPE");

        // 4. Valid PNG and DOCX with genuine magic bytes succeed
        byte[] validPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        var validPngRes = await client.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "receipt.png",
            contentType = "image/png",
            base64Content = Convert.ToBase64String(validPng)
        });
        validPngRes.StatusCode.Should().Be(HttpStatusCode.Created);

        byte[] validDocx = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00];
        var validDocxRes = await client.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "specification.docx",
            contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            base64Content = Convert.ToBase64String(validDocx)
        });
        validDocxRes.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UploadAttachment_WhenEicarSignatureInFileLargerThan1KB_Returns400BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        // Build a > 4 KB valid-header PDF buffer with the EICAR test signature embedded past the 1 KB boundary
        var prefix = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n%" + new string('A', 3072) + "\n");
        var eicar = System.Text.Encoding.ASCII.GetBytes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        var suffix = System.Text.Encoding.ASCII.GetBytes("\n%%EOF");

        byte[] largeInfectedPayload = new byte[prefix.Length + eicar.Length + suffix.Length];
        Buffer.BlockCopy(prefix, 0, largeInfectedPayload, 0, prefix.Length);
        Buffer.BlockCopy(eicar, 0, largeInfectedPayload, prefix.Length, eicar.Length);
        Buffer.BlockCopy(suffix, 0, largeInfectedPayload, prefix.Length + eicar.Length, suffix.Length);

        largeInfectedPayload.Length.Should().BeGreaterThan(3000);

        var response = await client.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "large-vendor-quote.pdf",
            contentType = "application/pdf",
            base64Content = Convert.ToBase64String(largeInfectedPayload)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("MALWARE_DETECTED");
    }

    [Fact]
    public async Task GetAttachment_WhenAuthorized_ReturnsNoSniffAndAttachmentDispositionHeaders()
    {
        var ownerClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");
        var pdfBytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 Clean Signed Document");

        // Include CRLF and quote characters in filename to verify header injection sanitization
        var uploadRes = await ownerClient.PostAsJsonAsync("/api/attachments", new
        {
            fileName = "vendor\"quote\r\nX-Injected: pwned.pdf",
            contentType = "application/pdf",
            base64Content = Convert.ToBase64String(pdfBytes)
        });
        uploadRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = await uploadRes.Content.ReadFromJsonAsync<WorkflowEngine.Application.Features.SmartForms.DTOs.FormAttachmentDto>();

        var getRes = await ownerClient.GetAsync($"/api/attachments/{uploaded!.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);

        getRes.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        getRes.Headers.Contains("X-Injected").Should().BeFalse();

        getRes.Content.Headers.ContentDisposition.Should().NotBeNull();
        getRes.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");

        string rawDisposition = getRes.Content.Headers.GetValues("Content-Disposition").Single();
        rawDisposition.Should().Be("attachment; filename=\"vendorquoteX-Injected pwned.pdf\"");
        rawDisposition.Should().NotContain("\r");
        rawDisposition.Should().NotContain("\n");

        (await getRes.Content.ReadAsByteArrayAsync()).Should().Equal(pdfBytes);
    }

    [Fact]
    public async Task SubmitSmartForm_WhenRequiredVisibleFieldMissing_Returns400BadRequest()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string formId = $"form-req-{Guid.NewGuid():N}"[..20];

        var createRes = await adminClient.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "RequiredValidationForm",
            title = "Required Validation Form",
            category = "Finance",
            fields = new object[]
            {
                new { id = "f1", name = "VendorName", label = "Vendor Name", type = "text", required = true, readOnly = false },
                new { id = "f2", name = "Amount", label = "Amount", type = "currency", required = true, readOnly = false },
                new
                {
                    id = "f3",
                    name = "ExecutiveJustification",
                    label = "Executive Justification",
                    type = "textarea",
                    required = true,
                    readOnly = false,
                    visibilityRule = new { field = "Amount", @operator = "greater_than", value = "5000" }
                }
            }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Amount = 8500 > 5000 makes ExecutiveJustification visible and required, while VendorName is also missing
        var submitRes = await adminClient.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["Amount"] = 8500
            },
            startLinkedWorkflow = false
        });

        submitRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorJson = await submitRes.Content.ReadAsStringAsync();
        errorJson.Should().Contain("VendorName");
        errorJson.Should().Contain("ExecutiveJustification");

        // Verify nothing was persisted
        var submissions = await adminClient.GetFromJsonAsync<List<WorkflowEngine.Application.Features.SmartForms.DTOs.SmartFormSubmissionDto>>(
            $"/api/smartforms/{formId}/submissions");
        submissions.Should().NotBeNull();
        submissions.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitSmartForm_WhenRequiredFieldIsHiddenByVisibilityRule_SucceedsAndStripsPhantomValue()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string formId = $"form-hid-{Guid.NewGuid():N}"[..20];

        var createRes = await adminClient.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "HiddenRequiredForm",
            title = "Hidden Required Form",
            category = "Finance",
            fields = new object[]
            {
                new { id = "f1", name = "VendorName", label = "Vendor Name", type = "text", required = true, readOnly = false },
                new { id = "f2", name = "Amount", label = "Amount", type = "currency", required = true, readOnly = false },
                new
                {
                    id = "f3",
                    name = "ExecutiveJustification",
                    label = "Executive Justification",
                    type = "textarea",
                    required = true,
                    readOnly = false,
                    visibilityRule = new { field = "Amount", @operator = "greater_than", value = "5000" }
                }
            }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Amount = 3000 <= 5000 -> ExecutiveJustification is hidden!
        // Client sends a phantom value for the hidden field AND an undeclared injected field: both must be stripped.
        var submitRes = await adminClient.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["VendorName"] = "Lenovo Enterprise",
                ["Amount"] = 3000,
                ["ExecutiveJustification"] = "Phantom hidden justification value",
                ["InjectedAdminFlag"] = true
            },
            startLinkedWorkflow = false
        });

        submitRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdSub = await submitRes.Content.ReadFromJsonAsync<WorkflowEngine.Application.Features.SmartForms.DTOs.SmartFormSubmissionDto>();
        createdSub.Should().NotBeNull();
        createdSub!.Data.Should().ContainKey("VendorName");
        createdSub.Data.Should().ContainKey("Amount");
        createdSub.Data.Should().NotContainKey("ExecutiveJustification");
        createdSub.Data.Should().NotContainKey("InjectedAdminFlag");

        var storedList = await adminClient.GetFromJsonAsync<List<WorkflowEngine.Application.Features.SmartForms.DTOs.SmartFormSubmissionDto>>(
            $"/api/smartforms/{formId}/submissions");
        storedList.Should().ContainSingle();
        storedList![0].Data.Should().NotContainKey("ExecutiveJustification");
        storedList[0].Data.Should().NotContainKey("InjectedAdminFlag");
    }

    [Fact]
    public async Task SubmitSmartForm_WhenInvalidDataTypeOrSelectOption_Returns400BadRequest()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string formId = $"form-typ-{Guid.NewGuid():N}"[..20];

        var createRes = await adminClient.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "StrictTypedForm",
            title = "Strict Typed Form",
            category = "Finance",
            fields = new object[]
            {
                new
                {
                    id = "f1",
                    name = "Department",
                    label = "Department",
                    type = "select",
                    required = true,
                    readOnly = false,
                    options = new[] { "Engineering", "Finance", "HR" }
                },
                new { id = "f2", name = "Amount", label = "Amount", type = "currency", required = true, readOnly = false },
                new { id = "f3", name = "DeliveryDate", label = "Delivery Date", type = "date", required = true, readOnly = false }
            }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var invalidRes = await adminClient.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["Department"] = "ShadowIT_UnlistedOption",
                ["Amount"] = "not-a-valid-number",
                ["DeliveryDate"] = "not-an-iso-date"
            },
            startLinkedWorkflow = false
        });

        invalidRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorBody = await invalidRes.Content.ReadAsStringAsync();
        errorBody.Should().Contain("Department");
        errorBody.Should().Contain("Amount");
        errorBody.Should().Contain("DeliveryDate");
    }

    [Fact]
    public async Task SubmitSmartForm_WhenPayloadExceeds256KB_Returns400BadRequest()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string formId = $"form-dos-{Guid.NewGuid():N}"[..20];

        var createRes = await adminClient.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "DosQuotaForm",
            title = "DoS Quota Form",
            category = "Security",
            fields = new object[]
            {
                new { id = "f1", name = "Notes", label = "Notes", type = "textarea", required = false, readOnly = false }
            }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Oversized > 256 KB submission payload
        string oversizedText = new string('X', 270 * 1024);
        var submitRes = await adminClient.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["Notes"] = oversizedText
            },
            startLinkedWorkflow = false
        });

        submitRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await submitRes.Content.ReadAsStringAsync()).Should().Contain("PAYLOAD_TOO_LARGE");

        // Also verify > 200 fields quota on form definition creation
        var tooManyFields = Enumerable.Range(1, 205)
            .Select(i => new { id = $"fld-{i}", name = $"Field_{i}", label = $"Field {i}", type = "text", required = false, readOnly = false })
            .ToArray();

        var createTooManyRes = await adminClient.PostAsJsonAsync("/api/smartforms", new
        {
            name = "OversizedSchemaForm",
            title = "Oversized Schema Form",
            category = "Security",
            fields = tooManyFields
        });
        createTooManyRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CompleteTask_WhenFormPatchModifiesReadOnlyField_Returns400BadRequest()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string formId = $"form-ro-{Guid.NewGuid():N}"[..20];

        var createFormRes = await adminClient.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "ReadOnlyProtectedTaskForm",
            title = "ReadOnly Protected Task Form",
            category = "Finance",
            fields = new object[]
            {
                new { id = "f1", name = "RequisitionCode", label = "Requisition Code", type = "text", required = false, readOnly = true, defaultValue = "REQ-2026-001" },
                new { id = "f2", name = "Amount", label = "Amount", type = "currency", required = true, readOnly = false }
            }
        });
        createFormRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var (_, taskId) = await CreateAndStartApprovalWorkflowAsync(
            adminClient,
            assignee: "karim.khmissi@corp.internal",
            assignmentType: "user",
            formId: formId,
            formMode: "editable",
            triggerVariables: new Dictionary<string, object?>
            {
                ["RequisitionCode"] = "REQ-2026-001",
                ["Amount"] = 5000
            });

        var assigneeClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        var completeRes = await assigneeClient.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "approve",
            comments = "Attempting to alter read-only requisition code",
            formPatch = new Dictionary<string, object?>
            {
                ["RequisitionCode"] = "REQ-TAMPERED-999",
                ["Amount"] = 4800
            }
        });

        completeRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorPayload = await completeRes.Content.ReadAsStringAsync();
        errorPayload.Should().Contain("READ_ONLY_FIELD_MODIFIED");
        errorPayload.Should().Contain("RequisitionCode");

        // Task must still be Pending
        var taskAfter = await adminClient.GetFromJsonAsync<WorkflowEngine.Application.Features.Tasks.DTOs.WorkflowTaskDto>(
            $"/api/tasks/{taskId}");
        taskAfter!.Status.Should().Be("Pending");
    }
}
