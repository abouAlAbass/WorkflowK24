using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.SmartForms.DTOs;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

/// <summary>
/// Sprint 0 / Step 0.4 - transactional submission, Idempotency-Key replay and optimistic concurrency (Version).
/// </summary>
public class AtomicSubmissionAndConcurrencyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AtomicSubmissionAndConcurrencyTests(TestWebApplicationFactory factory)
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

    private static string ApprovalGraphJson(string wfKey, string assignee) => $$"""
    {
      "id": "{{wfKey}}",
      "name": "Atomic Submission Workflow",
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
              "assignmentType": "user",
              "assignee": "{{assignee}}",
              "outcomes": [
                { "id": "approve", "label": "Approve", "color": "#059669" },
                { "id": "reject", "label": "Reject", "color": "#DC2626" }
              ],
              "dueDateDays": 2,
              "formId": null,
              "formMode": "readonly"
            }
          }
        },
        {
          "id": "end-1",
          "type": "end",
          "position": { "x": 450, "y": 0 },
          "data": { "nodeType": "end", "label": "Completed", "endData": { "status": "completed", "message": "Done" } }
        }
      ],
      "edges": [
        { "id": "e1", "source": "trig-start", "target": "appr-node-1" },
        { "id": "e2", "source": "appr-node-1", "target": "end-1", "sourceHandle": "approve" }
      ]
    }
    """;

    private static object[] SimpleVendorFields() => new object[]
    {
        new { id = "f1", name = "VendorName", label = "Vendor Name", type = "text", required = true, readOnly = false }
    };

    private static HttpRequestMessage SubmitRequest(string formId, object data, string? idempotencyKey, bool startWorkflow = true)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/api/smartforms/{formId}/submit")
        {
            Content = JsonContent.Create(new { data, startLinkedWorkflow = startWorkflow })
        };

        if (idempotencyKey != null)
        {
            msg.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return msg;
    }

    [Fact]
    public async Task SubmitSmartForm_WhenLinkedWorkflowFailsToStart_RollsBackSubmissionAndReturnsError()
    {
        var client = await CreateAuthenticatedClientAsync();

        // SmartObject bound to the form: its record must NOT survive the rollback either
        string soId = $"so-atom-{Guid.NewGuid():N}"[..18];
        var createSoRes = await client.PostAsJsonAsync("/api/smartobjects", new
        {
            id = soId,
            name = $"AtomicVendor{Guid.NewGuid():N}"[..24],
            displayName = "Atomic Vendor",
            description = "Rollback verification",
            category = "Finance",
            fields = new[]
            {
                new { name = "VendorName", type = "String", isPrimaryKey = false, isRequired = true }
            }
        });
        createSoRes.StatusCode.Should().Be(HttpStatusCode.Created);

        string formId = $"form-atom-{Guid.NewGuid():N}"[..20];
        var createFormRes = await client.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "AtomicRollbackForm",
            title = "Atomic Rollback Form",
            category = "Finance",
            linkedSmartObjectId = soId,
            linkedWorkflowKey = $"missing-workflow-{Guid.NewGuid():N}"[..28],
            fields = SimpleVendorFields(),
            publishImmediately = true
        });
        createFormRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.SendAsync(SubmitRequest(formId, new { VendorName = "TransactionalVendorCorp" }, null));

        // Explicit failure - never a silent 200/201
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("WORKFLOW_START_FAILED");

        // Submission rolled back
        var submissions = await client.GetFromJsonAsync<List<SmartFormSubmissionDto>>($"/api/smartforms/{formId}/submissions");
        submissions.Should().NotBeNull();
        submissions.Should().BeEmpty();

        // SmartObject record rolled back
        var recordsJson = await client.GetStringAsync($"/api/smartobjects/{soId}/records");
        recordsJson.Should().NotContain("TransactionalVendorCorp");

        // Submission counter rolled back
        var form = await client.GetFromJsonAsync<SmartFormDto>($"/api/smartforms/{formId}");
        form!.SubmissionsCount.Should().Be(0);
    }

    [Fact]
    public async Task SubmitSmartForm_WithSameIdempotencyKey_ReturnsExistingResultWithoutDuplicateInstance()
    {
        var client = await CreateAuthenticatedClientAsync();

        string wfKey = $"wf-idem-{Guid.NewGuid():N}"[..24];
        var createDefRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "Atomic Submission Workflow",
            graphJson = ApprovalGraphJson(wfKey, "karim.khmissi@corp.internal"),
            createdBy = "adel.sebti@corp.internal",
            category = "Finance"
        });
        createDefRes.StatusCode.Should().Be(HttpStatusCode.Created);

        string formId = $"form-idem-{Guid.NewGuid():N}"[..20];
        var createFormRes = await client.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "IdempotentSubmissionForm",
            title = "Idempotent Submission Form",
            category = "Finance",
            linkedWorkflowKey = wfKey,
            fields = SimpleVendorFields(),
            publishImmediately = true
        });
        createFormRes.StatusCode.Should().Be(HttpStatusCode.Created);

        string idempotencyKey = Guid.NewGuid().ToString();

        // First attempt creates the submission and starts exactly one workflow instance
        var first = await client.SendAsync(SubmitRequest(formId, new { VendorName = "Idempotent Vendor" }, idempotencyKey));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstDto = await first.Content.ReadFromJsonAsync<SmartFormSubmissionDto>();
        firstDto.Should().NotBeNull();
        firstDto!.LinkedInstanceId.Should().NotBeNullOrWhiteSpace();
        firstDto.IdempotentReplay.Should().BeFalse();

        // Replay with the same key: 200 OK, same SubmissionId, same WorkflowInstanceId, no side effects
        var replay = await client.SendAsync(SubmitRequest(formId, new { VendorName = "Idempotent Vendor" }, idempotencyKey));
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        var replayDto = await replay.Content.ReadFromJsonAsync<SmartFormSubmissionDto>();
        replayDto!.Id.Should().Be(firstDto.Id);
        replayDto.LinkedInstanceId.Should().Be(firstDto.LinkedInstanceId);
        replayDto.IdempotentReplay.Should().BeTrue();

        var submissions = await client.GetFromJsonAsync<List<SmartFormSubmissionDto>>($"/api/smartforms/{formId}/submissions");
        submissions.Should().ContainSingle();

        var instances = await client.GetFromJsonAsync<List<WorkflowInstanceDto>>("/api/instances");
        instances!.Count(i => i.WorkflowKey == wfKey).Should().Be(1);

        var form = await client.GetFromJsonAsync<SmartFormDto>($"/api/smartforms/{formId}");
        form!.SubmissionsCount.Should().Be(1);

        // A different key is a new logical submission
        var second = await client.SendAsync(SubmitRequest(formId, new { VendorName = "Idempotent Vendor" }, Guid.NewGuid().ToString()));
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondDto = await second.Content.ReadFromJsonAsync<SmartFormSubmissionDto>();
        secondDto!.Id.Should().NotBe(firstDto.Id);
        secondDto.LinkedInstanceId.Should().NotBe(firstDto.LinkedInstanceId);

        // Oversized keys are rejected explicitly
        var tooLong = await client.SendAsync(SubmitRequest(formId, new { VendorName = "x" }, new string('k', 129)));
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateSmartForm_WhenStaleVersionProvided_Returns409Conflict()
    {
        var client = await CreateAuthenticatedClientAsync();

        string formId = $"form-ver-{Guid.NewGuid():N}"[..20];
        var createRes = await client.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "VersionedForm",
            title = "Versioned Form v1",
            category = "Finance",
            fields = SimpleVendorFields(),
            publishImmediately = true
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<SmartFormDto>();
        created!.Version.Should().Be(1);

        // Designer A saves with the version it loaded -> accepted, version incremented
        var updateA = await client.PutAsJsonAsync($"/api/smartforms/{formId}", new
        {
            id = formId,
            title = "Versioned Form v2 (designer A)",
            expectedVersion = 1
        });
        updateA.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedA = await updateA.Content.ReadFromJsonAsync<SmartFormDto>();
        updatedA!.Version.Should().Be(2);

        // Designer B still holds version 1 -> 409 CONCURRENT_MODIFICATION and nothing is overwritten
        var updateB = await client.PutAsJsonAsync($"/api/smartforms/{formId}", new
        {
            id = formId,
            title = "Versioned Form (designer B - stale)",
            expectedVersion = 1
        });
        updateB.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await updateB.Content.ReadAsStringAsync()).Should().Contain("CONCURRENT_MODIFICATION");

        var current = await client.GetFromJsonAsync<SmartFormDto>($"/api/smartforms/{formId}");
        current!.Title.Should().Be("Versioned Form v2 (designer A)");
        current.Version.Should().Be(2);

        // Reloading and retrying with the fresh version succeeds
        var retry = await client.PutAsJsonAsync($"/api/smartforms/{formId}", new
        {
            id = formId,
            title = "Versioned Form v3 (designer B retry)",
            expectedVersion = 2
        });
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        (await retry.Content.ReadFromJsonAsync<SmartFormDto>())!.Version.Should().Be(3);

        // ExpectedVersion stays optional for backward compatibility
        var legacy = await client.PutAsJsonAsync($"/api/smartforms/{formId}", new
        {
            id = formId,
            title = "Versioned Form v4 (legacy client)"
        });
        legacy.StatusCode.Should().Be(HttpStatusCode.OK);
        (await legacy.Content.ReadFromJsonAsync<SmartFormDto>())!.Version.Should().Be(4);

        // Runtime submissions never bump the definition Version (no false conflicts for designers)
        var submit = await client.SendAsync(SubmitRequest(formId, new { VendorName = "Version Neutral" }, null, startWorkflow: false));
        submit.StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.GetFromJsonAsync<SmartFormDto>($"/api/smartforms/{formId}"))!.Version.Should().Be(4);
    }

    [Fact]
    public async Task CompleteTask_WhenConcurrentModificationOccurs_Returns409Conflict()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var assigneeClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");

        async Task<string> StartTaskAsync()
        {
            string wfKey = $"wf-cc-{Guid.NewGuid():N}"[..24];
            var defRes = await adminClient.PostAsJsonAsync("/api/definitions", new
            {
                workflowKey = wfKey,
                name = "Concurrency Workflow",
                graphJson = ApprovalGraphJson(wfKey, "karim.khmissi@corp.internal"),
                createdBy = "adel.sebti@corp.internal",
                category = "Finance"
            });
            defRes.StatusCode.Should().Be(HttpStatusCode.Created);

            var startRes = await adminClient.PostAsJsonAsync("/api/instances/start", new
            {
                workflowKey = wfKey,
                initiatorId = "adel.sebti@corp.internal",
                triggerVariables = new Dictionary<string, object?> { ["Amount"] = 100 }
            });
            startRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();

            var tasks = await adminClient.GetFromJsonAsync<List<WorkflowTaskDto>>($"/api/tasks/instance/{started!.InstanceId}");
            tasks.Should().ContainSingle();
            return tasks![0].TaskId;
        }

        // --- Part 1: a stale ExpectedVersion (task changed since the client loaded it) -> 409 ---
        string taskId = await StartTaskAsync();
        var loaded = await adminClient.GetFromJsonAsync<WorkflowTaskDto>($"/api/tasks/{taskId}");
        loaded!.Version.Should().Be(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            int affected = await db.Database.ExecuteSqlRawAsync(
                "UPDATE WorkflowTasks SET Version = Version + 1 WHERE TaskId = {0}", taskId);
            affected.Should().Be(1);
        }

        var staleRes = await assigneeClient.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "approve",
            comments = "Completing against a stale snapshot",
            expectedVersion = loaded.Version
        });
        staleRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await staleRes.Content.ReadAsStringAsync()).Should().Contain("CONCURRENT_MODIFICATION");

        var stillPending = await adminClient.GetFromJsonAsync<WorkflowTaskDto>($"/api/tasks/{taskId}");
        stillPending!.Status.Should().Be("Pending");
        stillPending.Version.Should().Be(2);

        // Retrying with the fresh version succeeds
        var freshRes = await assigneeClient.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "approve",
            expectedVersion = stillPending.Version
        });
        freshRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // --- Part 2: two actors race on the same row -> EF Core concurrency token rejects the loser ---
        string racedTaskId = await StartTaskAsync();

        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();

        var taskA = await dbA.WorkflowTasks.SingleAsync(t => t.TaskId == racedTaskId);
        var taskB = await dbB.WorkflowTasks.SingleAsync(t => t.TaskId == racedTaskId);

        taskB.Complete("approve", "other.approver@corp.internal");
        await dbB.SaveChangesAsync();

        taskA.Complete("reject", "karim.khmissi@corp.internal");
        Func<Task> loser = () => dbA.SaveChangesAsync();
        await loser.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
