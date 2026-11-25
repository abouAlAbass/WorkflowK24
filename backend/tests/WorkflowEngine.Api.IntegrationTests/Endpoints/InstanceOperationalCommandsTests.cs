using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

public class InstanceOperationalCommandsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public InstanceOperationalCommandsTests(TestWebApplicationFactory factory)
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

    private static string BuildApprovalGraphJson(string wfKey, string assigneeEmail) => $$"""
    {
      "id": "{{wfKey}}",
      "name": "Operational Command Test Workflow",
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
          "position": { "x": 200, "y": 0 },
          "data": {
            "nodeType": "approval",
            "label": "Operational Approval Task",
            "approvalData": {
              "assignmentType": "user",
              "assignee": "{{assigneeEmail}}",
              "outcomes": [
                { "id": "approve", "label": "Approve", "color": "#059669" },
                { "id": "reject", "label": "Reject", "color": "#DC2626" }
              ],
              "dueDateDays": 3,
              "formId": null,
              "formMode": "readonly"
            }
          }
        },
        {
          "id": "end-1",
          "type": "end",
          "position": { "x": 400, "y": 0 },
          "data": { "nodeType": "end", "label": "End", "endData": { "status": "completed" } }
        }
      ],
      "edges": [
        { "id": "e1", "source": "trig-start", "target": "appr-node-1" },
        { "id": "e2", "source": "appr-node-1", "target": "end-1", "sourceHandle": "approve" }
      ]
    }
    """;

    private async Task<(string InstanceId, string TaskId)> CreateWorkflowInstanceWithPendingTaskAsync(
        HttpClient client,
        string assigneeEmail = "adel.sebti@corp.internal")
    {
        string wfKey = $"op-wf-{Guid.NewGuid():N}"[..20];
        string graphJson = BuildApprovalGraphJson(wfKey, assigneeEmail);

        var createDefRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "Operational Test Definition",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Operations"
        });
        createDefRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var startRes = await client.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal"
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var startedResult = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        startedResult.Should().NotBeNull();

        var tasksRes = await client.GetAsync($"/api/tasks/instance/{startedResult!.InstanceId}");
        tasksRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var tasks = await tasksRes.Content.ReadFromJsonAsync<List<WorkflowTaskDto>>();
        tasks.Should().NotBeNull();
        tasks.Should().ContainSingle();

        return (startedResult.InstanceId, tasks!.First().TaskId);
    }

    [Fact]
    public async Task SuspendAndResume_RestoresPreviousStatusAndBlocksTaskCompletionWhileSuspended()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (instanceId, taskId) = await CreateWorkflowInstanceWithPendingTaskAsync(client);

        // 1. Suspend instance
        var suspendCommandId = Guid.NewGuid();
        var suspendRes = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/suspend", new
        {
            commandId = suspendCommandId,
            reason = "Auditing instance operations"
        });
        suspendRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var suspendBody = await suspendRes.Content.ReadFromJsonAsync<InstanceCommandResult>();
        suspendBody!.Status.Should().Be("Suspended");

        // Verify instance is Suspended in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var inst = await db.WorkflowInstances.FirstOrDefaultAsync(i => i.InstanceId == instanceId);
            inst.Should().NotBeNull();
            inst!.Status.Should().Be(WorkflowStatus.Suspended);
            inst.SuspendedFromStatus.Should().Be(WorkflowStatus.Running);
            inst.SuspendedAt.Should().NotBeNull();
        }

        // 2. Complete task while suspended -> MUST reject with 409 Conflict (INSTANCE_SUSPENDED)
        var completeRes = await client.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "approve",
            comments = "Trying to complete during suspension"
        });
        completeRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var completeError = await completeRes.Content.ReadAsStringAsync();
        completeError.Should().Contain("INSTANCE_SUSPENDED");

        // 3. Resume instance
        var resumeCommandId = Guid.NewGuid();
        var resumeRes = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/resume", new
        {
            commandId = resumeCommandId
        });
        resumeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var resumeBody = await resumeRes.Content.ReadFromJsonAsync<InstanceCommandResult>();
        resumeBody!.Status.Should().Be("Running");

        // Verify instance is restored in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var inst = await db.WorkflowInstances.FirstOrDefaultAsync(i => i.InstanceId == instanceId);
            inst!.Status.Should().Be(WorkflowStatus.Running);
            inst.SuspendedFromStatus.Should().BeNull();
            inst.SuspendedAt.Should().BeNull();
        }

        // 4. Complete task after resume -> now succeeds
        var completeAfterResumeRes = await client.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new
        {
            outcome = "approve",
            comments = "Completed after resume"
        });
        completeAfterResumeRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelInstance_RequiresReasonMin10Chars_AndCancelsOpenTasks()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (instanceId, taskId) = await CreateWorkflowInstanceWithPendingTaskAsync(client);

        // 1. Cancel with reason < 10 characters -> 400 Bad Request
        var badCancelRes = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/cancel", new
        {
            commandId = Guid.NewGuid(),
            reason = "short"
        });
        badCancelRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2. Cancel with reason >= 10 characters -> 200 OK
        var cancelCommandId = Guid.NewGuid();
        var goodCancelRes = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/cancel", new
        {
            commandId = cancelCommandId,
            reason = "Cancelled per customer written request"
        });
        goodCancelRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelBody = await goodCancelRes.Content.ReadFromJsonAsync<InstanceCommandResult>();
        cancelBody!.Status.Should().Be("Cancelled");

        // 3. Verify instance status is Cancelled and open task is Cancelled
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var inst = await db.WorkflowInstances.Include(i => i.Tasks).FirstOrDefaultAsync(i => i.InstanceId == instanceId);
            inst!.Status.Should().Be(WorkflowStatus.Cancelled);
            inst.CompletedAt.Should().NotBeNull();

            var task = inst.Tasks.FirstOrDefault(t => t.TaskId == taskId);
            task.Should().NotBeNull();
            task!.Status.Should().Be(TaskStatus.Cancelled);
        }

        // 4. Any further command on Cancelled instance -> 409 INVALID_TRANSITION
        var postCancelSuspend = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/suspend", new
        {
            commandId = Guid.NewGuid(),
            reason = "Suspend after cancel"
        });
        postCancelSuspend.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var errJson = await postCancelSuspend.Content.ReadAsStringAsync();
        errJson.Should().Contain("INVALID_TRANSITION");
    }

    [Fact]
    public async Task RetryInstance_WhenFaulted_TransitionsToRunning_AndRejectsNonFaultedWith409()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (instanceId, _) = await CreateWorkflowInstanceWithPendingTaskAsync(client);

        // 1. Instance is currently Running -> Retry must be rejected with 409 INVALID_TRANSITION
        var retryRunningRes = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/retry", new
        {
            commandId = Guid.NewGuid(),
            mode = "retry_failed_node"
        });
        retryRunningRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await retryRunningRes.Content.ReadAsStringAsync()).Should().Contain("INVALID_TRANSITION");

        // 2. Set instance to Faulted in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var inst = await db.WorkflowInstances.FirstOrDefaultAsync(i => i.InstanceId == instanceId);
            inst!.RecordFailure("appr-node-1", "approval", "SIMULATED_ERR", "Simulated node failure", null, null);
            await db.SaveChangesAsync();
        }

        // 3. Retry when Faulted -> transitions to Running
        var retryCommandId = Guid.NewGuid();
        var retryRes = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/retry", new
        {
            commandId = retryCommandId,
            mode = "retry_failed_node"
        });
        retryRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var retryBody = await retryRes.Content.ReadFromJsonAsync<InstanceCommandResult>();
        retryBody!.Status.Should().Be("Running");

        // Verify status in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var inst = await db.WorkflowInstances.FirstOrDefaultAsync(i => i.InstanceId == instanceId);
            inst!.Status.Should().Be(WorkflowStatus.Running);
        }
    }

    [Fact]
    public async Task OperationalCommand_WithSameCommandId_IsIdempotentAndWritesSingleEvent()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (instanceId, _) = await CreateWorkflowInstanceWithPendingTaskAsync(client);

        var fixedCommandId = Guid.NewGuid();

        // Call 1: First suspend call
        var res1 = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/suspend", new
        {
            commandId = fixedCommandId,
            reason = "Idempotency test suspend"
        });
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var body1 = await res1.Content.ReadFromJsonAsync<InstanceCommandResult>();
        body1!.CommandId.Should().Be(fixedCommandId);

        // Call 2: Replay identical suspend commandId
        var res2 = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/suspend", new
        {
            commandId = fixedCommandId,
            reason = "Idempotency test suspend replayed"
        });
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body2 = await res2.Content.ReadFromJsonAsync<InstanceCommandResult>();
        body2!.CommandId.Should().Be(fixedCommandId);

        // Verify database: exactly 1 command log entry and exactly 1 Instance.Suspended event
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var cmdLogs = await db.CommandLogs.Where(c => c.CommandId == fixedCommandId).ToListAsync();
            cmdLogs.Should().ContainSingle();

            var events = await db.InstanceEvents
                .Where(e => e.InstanceId == instanceId && e.Type == "Instance.Suspended")
                .ToListAsync();
            events.Should().ContainSingle();
            events.First().Seq.Should().Be(1);
        }
    }

    [Fact]
    public async Task OperationalCommand_WhenUnauthorizedUser_Returns403Forbidden()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var (instanceId, _) = await CreateWorkflowInstanceWithPendingTaskAsync(adminClient);

        // Ensure an active End User exists
        string endUserEmail = "test.enduser@corp.internal";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var existing = await db.SystemUsers.FirstOrDefaultAsync(u => u.Email == endUserEmail);
            if (existing == null)
            {
                var newUser = new SystemUser(
                    $"usr-{Guid.NewGuid():N}"[..18],
                    "Test EndUser",
                    endUserEmail,
                    "End User",
                    "Support",
                    "Active",
                    "/avatars/default.svg",
                    WorkflowEngine.Domain.Security.PasswordHasher.HashPassword(DatabaseInitializer.DefaultSeedPassword));
                db.SystemUsers.Add(newUser);
                await db.SaveChangesAsync();
            }
        }

        // Login as unauthorized End User
        var unauthorizedClient = await CreateAuthenticatedClientAsync(
            email: endUserEmail,
            password: DatabaseInitializer.DefaultSeedPassword);

        // Attempt suspend
        var suspendRes = await unauthorizedClient.PostAsJsonAsync($"/api/instances/{instanceId}/commands/suspend", new
        {
            commandId = Guid.NewGuid(),
            reason = "Unauthorized attempt"
        });
        suspendRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Attempt cancel
        var cancelRes = await unauthorizedClient.PostAsJsonAsync($"/api/instances/{instanceId}/commands/cancel", new
        {
            commandId = Guid.NewGuid(),
            reason = "Unauthorized cancel attempt"
        });
        cancelRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Attempt resume
        var resumeRes = await unauthorizedClient.PostAsJsonAsync($"/api/instances/{instanceId}/commands/resume", new
        {
            commandId = Guid.NewGuid()
        });
        resumeRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Attempt retry
        var retryRes = await unauthorizedClient.PostAsJsonAsync($"/api/instances/{instanceId}/commands/retry", new
        {
            commandId = Guid.NewGuid(),
            mode = "retry_failed_node"
        });
        retryRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
