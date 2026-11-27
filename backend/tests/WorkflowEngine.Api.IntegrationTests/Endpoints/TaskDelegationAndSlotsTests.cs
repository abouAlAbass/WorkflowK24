using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Security;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

/// <summary>
/// Jalon C / Étape C.1 — Task delegation, recall, reassignment, Out-of-Office rules,
/// cycle and depth validation, group slots (Claim/Release), and SLA escalation.
/// </summary>
public class TaskDelegationAndSlotsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TaskDelegationAndSlotsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "adel.sebti@corp.internal",
        string password = DatabaseInitializer.DefaultSeedPassword)
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK, $"login failed for {email}");

        var body = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private async Task<string> EnsureTestUserAsync(string email, string name, string role, string group)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        var existing = db.SystemUsers.FirstOrDefault(u => u.Email == email.ToLowerInvariant());
        if (existing == null)
        {
            var user = new SystemUser(
                $"usr-test-{Guid.NewGuid():N}"[..24],
                name,
                email,
                role,
                group,
                "Active",
                "/avatars/user-2.svg",
                PasswordHasher.HashPassword(DatabaseInitializer.DefaultSeedPassword));
            db.SystemUsers.Add(user);
            await db.SaveChangesAsync();
        }
        return email;
    }

    private static string BuildApprovalGraphJson(
        string wfKey,
        string assignmentType,
        string assignee,
        string? resolutionPolicy = "single",
        int? quorumCount = null,
        DateTime? dueAt = null,
        string? escalationTarget = null,
        string? escalationTargetType = null)
    {
        string dueAtJson = dueAt.HasValue ? $"\"{dueAt.Value:O}\"" : "null";
        string quorumJson = quorumCount.HasValue ? quorumCount.Value.ToString() : "null";
        string escTargetJson = escalationTarget != null ? $"\"{escalationTarget}\"" : "null";
        string escTypeJson = escalationTargetType != null ? $"\"{escalationTargetType}\"" : "null";

        return $$"""
        {
          "id": "{{wfKey}}",
          "name": "Delegation & Slots Workflow",
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
                "label": "Manager Approval",
                "approvalData": {
                  "assignmentType": "{{assignmentType}}",
                  "assignee": "{{assignee}}",
                  "resolutionPolicy": "{{resolutionPolicy}}",
                  "quorumCount": {{quorumJson}},
                  "dueDateDays": 2,
                  "dueAt": {{dueAtJson}},
                  "escalationTarget": {{escTargetJson}},
                  "escalationTargetType": {{escTypeJson}},
                  "outcomes": [
                    { "id": "approve", "label": "Approve", "color": "#059669" },
                    { "id": "reject", "label": "Reject", "color": "#DC2626" }
                  ]
                }
              }
            },
            {
              "id": "end-1",
              "type": "end",
              "position": { "x": 450, "y": 0 },
              "data": { "nodeType": "end", "label": "Done", "endData": { "status": "completed", "message": "Done" } }
            }
          ],
          "edges": [
            { "id": "e1", "source": "trig-start", "target": "appr-node-1" },
            { "id": "e2", "source": "appr-node-1", "target": "end-1", "sourceHandle": "approve" }
          ]
        }
        """;
    }

    private async Task<(string InstanceId, WorkflowTaskDto Task)> StartApprovalWorkflowAsync(
        HttpClient adminClient,
        string wfKey,
        string graphJson)
    {
        var createDef = await adminClient.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = $"Workflow {wfKey}",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Finance"
        });
        createDef.StatusCode.Should().Be(HttpStatusCode.Created);

        var startRes = await adminClient.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal"
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceDto>();
        started.Should().NotBeNull();

        var tasks = await adminClient.GetFromJsonAsync<List<WorkflowTaskDto>>($"/api/tasks/instance/{started!.InstanceId}");
        tasks.Should().NotBeNull();
        tasks.Should().ContainSingle();
        return (started.InstanceId, tasks![0]);
    }

    [Fact]
    public async Task DelegateTask_RecordsAssignmentHistoryAndAuditsCompletedByAndOnBehalfOf()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        var karimClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");
        var mohamedClient = await CreateAuthenticatedClientAsync("m.fetni@enterprise.internal");

        string wfKey = $"wf-del-{Guid.NewGuid():N}"[..24];
        var (instanceId, task) = await StartApprovalWorkflowAsync(
            adminClient,
            wfKey,
            BuildApprovalGraphJson(wfKey, "user", "karim.khmissi@corp.internal"));

        // 1. Delegating to an inactive/unauthorized user returns 422 DELEGATE_NOT_AUTHORIZED
        var invalidDelegateRes = await karimClient.PostAsJsonAsync($"/api/tasks/{task.TaskId}/delegate", new
        {
            targetAssigneeId = "y.benali@enterprise.internal",
            reason = "Should fail for inactive user"
        });
        invalidDelegateRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalidDelegateRes.Content.ReadAsStringAsync()).Should().Contain("DELEGATE_NOT_AUTHORIZED");

        // 2. Suspend the instance: delegation MUST still be allowed, while completion MUST be blocked with 409 INSTANCE_SUSPENDED
        var suspendMsg = new HttpRequestMessage(HttpMethod.Post, $"/api/instances/{instanceId}/commands/suspend")
        {
            Content = JsonContent.Create(new { commandId = Guid.NewGuid().ToString(), reason = "Audit hold" })
        };
        suspendMsg.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await adminClient.SendAsync(suspendMsg)).StatusCode.Should().Be(HttpStatusCode.OK);

        var delegateRes = await karimClient.PostAsJsonAsync($"/api/tasks/{task.TaskId}/delegate", new
        {
            targetAssigneeId = "m.fetni@enterprise.internal",
            reason = "Covering during business travel"
        });
        delegateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var delegatedTask = await delegateRes.Content.ReadFromJsonAsync<WorkflowTaskDto>();
        delegatedTask!.AssigneeId.Should().Be("m.fetni@enterprise.internal");
        delegatedTask.OriginalAssigneeId.Should().Be("karim.khmissi@corp.internal");
        delegatedTask.OnBehalfOfUserId.Should().Be("karim.khmissi@corp.internal");

        // Completing while suspended is still blocked
        var blockedComplete = await mohamedClient.PostAsJsonAsync($"/api/tasks/{task.TaskId}/complete", new
        {
            outcome = "approve",
            comments = "Blocked while suspended"
        });
        blockedComplete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blockedComplete.Content.ReadAsStringAsync()).Should().Contain("INSTANCE_SUSPENDED");

        // Resume instance so delegate can complete the task
        var resumeMsg = new HttpRequestMessage(HttpMethod.Post, $"/api/instances/{instanceId}/commands/resume")
        {
            Content = JsonContent.Create(new { commandId = Guid.NewGuid().ToString() })
        };
        resumeMsg.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await adminClient.SendAsync(resumeMsg)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Delegate completes the task and audit fields CompletedByUserId + OnBehalfOfUserId are recorded
        var completeRes = await mohamedClient.PostAsJsonAsync($"/api/tasks/{task.TaskId}/complete", new
        {
            outcome = "approve",
            comments = "Approved on behalf of Karim"
        });
        completeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var completedResult = await completeRes.Content.ReadFromJsonAsync<TaskCompletedResult>();
        completedResult!.CompletedByUserId.Should().Be("m.fetni@enterprise.internal");
        completedResult.OnBehalfOfUserId.Should().Be("karim.khmissi@corp.internal");

        var persistedTask = await adminClient.GetFromJsonAsync<WorkflowTaskDto>($"/api/tasks/{task.TaskId}");
        persistedTask!.CompletedByUserId.Should().Be("m.fetni@enterprise.internal");
        persistedTask.OnBehalfOfUserId.Should().Be("karim.khmissi@corp.internal");

        // 4. Append-only assignment history contains initial + delegation entries
        var assignments = await adminClient.GetFromJsonAsync<List<TaskAssignmentDto>>($"/api/tasks/{task.TaskId}/assignments");
        assignments.Should().NotBeNull();
        assignments!.Select(a => a.Action).Should().ContainInOrder("initial", "delegation");
        assignments.Last().FromAssigneeId.Should().Be("karim.khmissi@corp.internal");
        assignments.Last().ToAssigneeId.Should().Be("m.fetni@enterprise.internal");
        assignments.Last().ActorUserId.Should().Be("karim.khmissi@corp.internal");
    }

    [Fact]
    public async Task CreateDelegationRule_WhenCircularChainDetected_Returns422()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string scope = $"wf-cycle-{Guid.NewGuid():N}"[..22];
        var validFrom = DateTime.UtcNow.AddHours(-1);
        var validTo = DateTime.UtcNow.AddDays(5);

        string userA = await EnsureTestUserAsync($"cycle.a.{Guid.NewGuid():N}@corp.internal"[..36], "Cycle User A", "Process Manager", "Ops");
        string userB = await EnsureTestUserAsync($"cycle.b.{Guid.NewGuid():N}@corp.internal"[..36], "Cycle User B", "Process Manager", "Ops");
        string userC = await EnsureTestUserAsync($"cycle.c.{Guid.NewGuid():N}@corp.internal"[..36], "Cycle User C", "Process Manager", "Ops");

        var createdIds = new List<string>();
        try
        {
            // A -> B
            var r1 = await adminClient.PostAsJsonAsync("/api/delegation-rules", new
            {
                delegatorId = userA,
                delegateId = userB,
                scope,
                validFrom,
                validTo
            });
            r1.StatusCode.Should().Be(HttpStatusCode.Created);
            createdIds.Add((await r1.Content.ReadFromJsonAsync<DelegationRuleDto>())!.Id);

            // B -> C
            var r2 = await adminClient.PostAsJsonAsync("/api/delegation-rules", new
            {
                delegatorId = userB,
                delegateId = userC,
                scope,
                validFrom,
                validTo
            });
            r2.StatusCode.Should().Be(HttpStatusCode.Created);
            createdIds.Add((await r2.Content.ReadFromJsonAsync<DelegationRuleDto>())!.Id);

            // C -> A creates a cycle A -> B -> C -> A => 422 DELEGATION_CYCLE_DETECTED
            var r3 = await adminClient.PostAsJsonAsync("/api/delegation-rules", new
            {
                delegatorId = userC,
                delegateId = userA,
                scope,
                validFrom,
                validTo
            });
            r3.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await r3.Content.ReadAsStringAsync()).Should().Contain("DELEGATION_CYCLE_DETECTED");
        }
        finally
        {
            foreach (var id in createdIds)
            {
                await adminClient.DeleteAsync($"/api/delegation-rules/{id}");
            }
        }
    }

    [Fact]
    public async Task CreateTask_WhenActiveDelegationRuleExists_AutoAssignsToDelegate()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string wfKey = $"wf-ooo-{Guid.NewGuid():N}"[..24];

        string delegator = await EnsureTestUserAsync($"ooo.orig.{Guid.NewGuid():N}@corp.internal"[..38], "OOO Original", "Process Manager", "Finance");
        string delegateUser = await EnsureTestUserAsync($"ooo.del.{Guid.NewGuid():N}@corp.internal"[..37], "OOO Delegate", "Process Manager", "Finance");

        var ruleRes = await adminClient.PostAsJsonAsync("/api/delegation-rules", new
        {
            delegatorId = delegator,
            delegateId = delegateUser,
            scope = wfKey,
            validFrom = DateTime.UtcNow.AddHours(-2),
            validTo = DateTime.UtcNow.AddDays(3),
            reason = "Annual leave"
        });
        ruleRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdRule = await ruleRes.Content.ReadFromJsonAsync<DelegationRuleDto>();

        try
        {
            var (_, task) = await StartApprovalWorkflowAsync(
                adminClient,
                wfKey,
                BuildApprovalGraphJson(wfKey, "user", delegator));

            task.OriginalAssigneeId.Should().Be(delegator);
            task.AssigneeId.Should().Be(delegateUser);
            task.OnBehalfOfUserId.Should().Be(delegator);

            var assignments = await adminClient.GetFromJsonAsync<List<TaskAssignmentDto>>($"/api/tasks/{task.TaskId}/assignments");
            assignments.Should().NotBeNull();
            assignments!.Select(a => a.Action).Should().ContainInOrder("initial", "auto_delegation");
        }
        finally
        {
            await adminClient.DeleteAsync($"/api/delegation-rules/{createdRule!.Id}");
        }
    }

    [Fact]
    public async Task GroupTask_ClaimAndRelease_EnforcesExclusiveLock()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string groupName = $"ClaimGroup-{Guid.NewGuid():N}"[..20];
        string user1Email = await EnsureTestUserAsync($"grp1.{Guid.NewGuid():N}"[..14] + "@corp.internal", "Group Member 1", "Process Manager", groupName);
        string user2Email = await EnsureTestUserAsync($"grp2.{Guid.NewGuid():N}"[..14] + "@corp.internal", "Group Member 2", "Process Manager", groupName);

        var user1Client = await CreateAuthenticatedClientAsync(user1Email);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email);

        string wfKey = $"wf-claim-{Guid.NewGuid():N}"[..24];
        var (_, task) = await StartApprovalWorkflowAsync(
            adminClient,
            wfKey,
            BuildApprovalGraphJson(wfKey, "group", groupName, resolutionPolicy: "claim"));

        // 1. User 1 claims the group task
        var claim1 = await user1Client.PostAsJsonAsync($"/api/tasks/{task.TaskId}/claim", new { });
        string claim1Body = await claim1.Content.ReadAsStringAsync();
        claim1.StatusCode.Should().Be(HttpStatusCode.OK, $"body={claim1Body}, taskAssignee={task.AssigneeType}:{task.AssigneeId}");
        var claimedTask = await claim1.Content.ReadFromJsonAsync<WorkflowTaskDto>();
        claimedTask!.ClaimedByUserId.Should().Be(user1Email);

        // 2. User 2 cannot claim while locked by User 1 -> 409 TASK_ALREADY_CLAIMED
        var claim2Blocked = await user2Client.PostAsJsonAsync($"/api/tasks/{task.TaskId}/claim", new { });
        claim2Blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await claim2Blocked.Content.ReadAsStringAsync()).Should().Contain("TASK_ALREADY_CLAIMED");

        // 3. User 2 cannot complete while locked by User 1 -> 409 TASK_CLAIMED_BY_ANOTHER_USER
        var complete2Blocked = await user2Client.PostAsJsonAsync($"/api/tasks/{task.TaskId}/complete", new
        {
            outcome = "approve",
            comments = "Unauthorized attempt while claimed"
        });
        complete2Blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await complete2Blocked.Content.ReadAsStringAsync()).Should().Contain("TASK_CLAIMED_BY_ANOTHER_USER");

        // 4. User 1 releases the claim
        var release1 = await user1Client.PostAsJsonAsync($"/api/tasks/{task.TaskId}/release", new
        {
            reason = "Handing off to colleague"
        });
        release1.StatusCode.Should().Be(HttpStatusCode.OK);
        var releasedTask = await release1.Content.ReadFromJsonAsync<WorkflowTaskDto>();
        releasedTask!.ClaimedByUserId.Should().BeNull();

        // 5. User 2 can now claim and complete the task
        var claim2 = await user2Client.PostAsJsonAsync($"/api/tasks/{task.TaskId}/claim", new { });
        claim2.StatusCode.Should().Be(HttpStatusCode.OK);

        var complete2 = await user2Client.PostAsJsonAsync($"/api/tasks/{task.TaskId}/complete", new
        {
            outcome = "approve",
            comments = "Completed after claim transfer"
        });
        complete2.StatusCode.Should().Be(HttpStatusCode.OK);

        var assignments = await adminClient.GetFromJsonAsync<List<TaskAssignmentDto>>($"/api/tasks/{task.TaskId}/assignments");
        assignments!.Select(a => a.Action).Should().ContainInOrder("initial", "claim", "release", "claim");
    }

    [Fact]
    public async Task EscalationCheck_WhenTaskPastDueDate_EscalatesTaskAndWritesEvent()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string wfKey = $"wf-sla-{Guid.NewGuid():N}"[..24];
        DateTime pastDueAt = DateTime.UtcNow.AddHours(-3);

        var (instanceId, task) = await StartApprovalWorkflowAsync(
            adminClient,
            wfKey,
            BuildApprovalGraphJson(
                wfKey,
                "user",
                "karim.khmissi@corp.internal",
                dueAt: pastDueAt,
                escalationTarget: "adel.sebti@corp.internal",
                escalationTargetType: "User"));

        task.AssigneeId.Should().Be("karim.khmissi@corp.internal");

        // Run SLA escalation check
        var checkRes = await adminClient.PostAsync("/api/tasks/escalations/check", null);
        checkRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkResult = await checkRes.Content.ReadFromJsonAsync<EscalationCheckResult>();
        checkResult!.EscalatedTaskIds.Should().Contain(task.TaskId);

        // Verify task was escalated to the configured escalation target
        var updatedTask = await adminClient.GetFromJsonAsync<WorkflowTaskDto>($"/api/tasks/{task.TaskId}");
        updatedTask!.AssigneeId.Should().Be("adel.sebti@corp.internal");
        updatedTask.OriginalAssigneeId.Should().Be("karim.khmissi@corp.internal");
        updatedTask.EscalatedAt.Should().NotBeNull();

        // Verify assignment history has "escalation"
        var assignments = await adminClient.GetFromJsonAsync<List<TaskAssignmentDto>>($"/api/tasks/{task.TaskId}/assignments");
        assignments!.Select(a => a.Action).Should().Contain("escalation");

        // Verify InstanceEvent "Task.Escalated" was written
        var eventsJson = await adminClient.GetStringAsync($"/api/instances/{instanceId}/events");
        using var doc = JsonDocument.Parse(eventsJson);
        var events = doc.RootElement.GetProperty("events").EnumerateArray().ToList();
        events.Should().Contain(e => e.GetProperty("type").GetString() == "Task.Escalated");
    }
}
