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
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

/// <summary>
/// Jalon D — Step D.1 integration tests:
/// Token execution model, Split/Join static validation, deterministic variable merging,
/// Join any sibling cancellation, concurrent branch completion (exactly-once Join firing),
/// and dynamic_foreach collection fan-out.
/// </summary>
public class ParallelSplitJoinTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ParallelSplitJoinTests(TestWebApplicationFactory factory)
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

    private async Task<HttpResponseMessage> PublishDefinitionAsync(HttpClient client, string wfKey, string graphJson)
    {
        return await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = $"Parallel Test {wfKey}",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Operations"
        });
    }

    private async Task<WorkflowInstanceStartedResult> StartInstanceAsync(
        HttpClient client,
        string wfKey,
        Dictionary<string, object>? triggerVariables = null)
    {
        var res = await client.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal",
            triggerVariables
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await res.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        started.Should().NotBeNull();
        return started!;
    }

    [Fact]
    public async Task PublishWorkflow_WithUnbalancedSplitJoinOrUnresolvedVariableConflict_Returns422()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Unbalanced Split (no matching Join)
        string unbalancedKey = $"par-unbal-{Guid.NewGuid():N}"[..20];
        string unbalancedGraph = $$"""
        {
          "id": "{{unbalancedKey}}",
          "name": "Unbalanced Split",
          "version": "1.0",
          "nodes": [
            { "id": "start", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            { "id": "split-1", "type": "parallelSplit", "position": { "x": 150, "y": 0 }, "data": { "nodeType": "parallelSplit", "label": "Split", "splitData": { "mode": "all" } } },
            { "id": "end-1", "type": "end", "position": { "x": 350, "y": 0 }, "data": { "nodeType": "end", "label": "End" } }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "split-1" },
            { "id": "e2", "source": "split-1", "target": "end-1" }
          ]
        }
        """;
        var unbalancedRes = await PublishDefinitionAsync(client, unbalancedKey, unbalancedGraph);
        unbalancedRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 2. Boundary crossing edge (edge from inside Split/Join directly to outside End)
        string boundaryKey = $"par-bound-{Guid.NewGuid():N}"[..20];
        string boundaryGraph = $$"""
        {
          "id": "{{boundaryKey}}",
          "name": "Boundary Violation",
          "version": "1.0",
          "nodes": [
            { "id": "start", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            { "id": "split-1", "type": "parallelSplit", "position": { "x": 100, "y": 0 }, "data": { "nodeType": "parallelSplit", "label": "Split", "splitData": { "mode": "all" } } },
            { "id": "b1", "type": "sendEmail", "position": { "x": 200, "y": -50 }, "data": { "nodeType": "sendEmail", "label": "B1" } },
            { "id": "b2", "type": "sendEmail", "position": { "x": 200, "y": 50 }, "data": { "nodeType": "sendEmail", "label": "B2" } },
            { "id": "join-1", "type": "parallelJoin", "position": { "x": 300, "y": 0 }, "data": { "nodeType": "parallelJoin", "label": "Join", "joinData": { "policy": "all" } } },
            { "id": "end-1", "type": "end", "position": { "x": 400, "y": 0 }, "data": { "nodeType": "end", "label": "End" } }
          ],
          "edges": [
            { "id": "e0", "source": "start", "target": "split-1" },
            { "id": "e1", "source": "split-1", "target": "b1" },
            { "id": "e2", "source": "split-1", "target": "b2" },
            { "id": "e3", "source": "b1", "target": "join-1" },
            { "id": "e4", "source": "b2", "target": "join-1" },
            { "id": "e-escape", "source": "b1", "target": "end-1" },
            { "id": "e5", "source": "join-1", "target": "end-1" }
          ]
        }
        """;
        var boundaryRes = await PublishDefinitionAsync(client, boundaryKey, boundaryGraph);
        boundaryRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 3. Invalid n_of_m where n > m
        string quorumKey = $"par-quor-{Guid.NewGuid():N}"[..20];
        string quorumGraph = $$"""
        {
          "id": "{{quorumKey}}",
          "name": "Invalid Quorum",
          "version": "1.0",
          "nodes": [
            { "id": "start", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            { "id": "split-1", "type": "parallelSplit", "position": { "x": 100, "y": 0 }, "data": { "nodeType": "parallelSplit", "label": "Split", "splitData": { "mode": "all" } } },
            { "id": "b1", "type": "sendEmail", "position": { "x": 200, "y": -50 }, "data": { "nodeType": "sendEmail", "label": "B1" } },
            { "id": "b2", "type": "sendEmail", "position": { "x": 200, "y": 50 }, "data": { "nodeType": "sendEmail", "label": "B2" } },
            { "id": "join-1", "type": "parallelJoin", "position": { "x": 300, "y": 0 }, "data": { "nodeType": "parallelJoin", "label": "Join", "joinData": { "policy": "n_of_m", "quorumCount": 3 } } },
            { "id": "end-1", "type": "end", "position": { "x": 400, "y": 0 }, "data": { "nodeType": "end", "label": "End" } }
          ],
          "edges": [
            { "id": "e0", "source": "start", "target": "split-1" },
            { "id": "e1", "source": "split-1", "target": "b1" },
            { "id": "e2", "source": "split-1", "target": "b2" },
            { "id": "e3", "source": "b1", "target": "join-1" },
            { "id": "e4", "source": "b2", "target": "join-1" },
            { "id": "e5", "source": "join-1", "target": "end-1" }
          ]
        }
        """;
        var quorumRes = await PublishDefinitionAsync(client, quorumKey, quorumGraph);
        quorumRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 4. Unresolved variable write conflict across branches (no explicit merge_strategy)
        string conflictKey = $"par-conf-{Guid.NewGuid():N}"[..20];
        string conflictGraph = $$"""
        {
          "id": "{{conflictKey}}",
          "name": "Unresolved Variable Conflict",
          "version": "1.0",
          "nodes": [
            { "id": "start", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            { "id": "split-1", "type": "parallelSplit", "position": { "x": 100, "y": 0 }, "data": { "nodeType": "parallelSplit", "label": "Split", "splitData": { "mode": "all" } } },
            { "id": "b1", "type": "sendEmail", "position": { "x": 200, "y": -50 }, "data": { "nodeType": "sendEmail", "label": "B1", "setVariables": { "sharedTotal": 10 } } },
            { "id": "b2", "type": "sendEmail", "position": { "x": 200, "y": 50 }, "data": { "nodeType": "sendEmail", "label": "B2", "setVariables": { "sharedTotal": 20 } } },
            { "id": "join-1", "type": "parallelJoin", "position": { "x": 300, "y": 0 }, "data": { "nodeType": "parallelJoin", "label": "Join", "joinData": { "policy": "all" } } },
            { "id": "end-1", "type": "end", "position": { "x": 400, "y": 0 }, "data": { "nodeType": "end", "label": "End" } }
          ],
          "edges": [
            { "id": "e0", "source": "start", "target": "split-1" },
            { "id": "e1", "source": "split-1", "target": "b1" },
            { "id": "e2", "source": "split-1", "target": "b2" },
            { "id": "e3", "source": "b1", "target": "join-1" },
            { "id": "e4", "source": "b2", "target": "join-1" },
            { "id": "e5", "source": "join-1", "target": "end-1" }
          ]
        }
        """;
        var conflictRes = await PublishDefinitionAsync(client, conflictKey, conflictGraph);
        conflictRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string conflictBody = await conflictRes.Content.ReadAsStringAsync();
        conflictBody.Should().Contain("sharedTotal");
    }

    [Fact]
    public async Task ParallelSplitJoinAll_WaitsForAllBranchesAndMergesVariablesDeterministically()
    {
        var client = await CreateAuthenticatedClientAsync();
        string wfKey = $"par-all-{Guid.NewGuid():N}"[..20];
        string assignee = "adel.sebti@corp.internal";

        string graphJson = $$"""
        {
          "id": "{{wfKey}}",
          "name": "Parallel All Merge",
          "version": "1.0",
          "nodes": [
            { "id": "start", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            { "id": "split-1", "type": "parallelSplit", "position": { "x": 120, "y": 0 }, "data": { "nodeType": "parallelSplit", "label": "Split All", "splitData": { "mode": "all" } } },
            {
              "id": "appr-a", "type": "approval", "position": { "x": 260, "y": -80 },
              "data": {
                "nodeType": "approval", "label": "Branch A Approval",
                "approvalData": {
                  "assignmentType": "user", "assignee": "{{assignee}}", "formMode": "editable",
                  "outcomes": [{ "id": "approve", "label": "Approve", "color": "#059669" }],
                  "outputVariables": ["approvers", "score", "branchAOnly"]
                }
              }
            },
            {
              "id": "appr-b", "type": "approval", "position": { "x": 260, "y": 80 },
              "data": {
                "nodeType": "approval", "label": "Branch B Approval",
                "approvalData": {
                  "assignmentType": "user", "assignee": "{{assignee}}", "formMode": "editable",
                  "outcomes": [{ "id": "approve", "label": "Approve", "color": "#059669" }],
                  "outputVariables": ["approvers", "score", "branchBOnly"]
                }
              }
            },
            {
              "id": "join-1", "type": "parallelJoin", "position": { "x": 420, "y": 0 },
              "data": {
                "nodeType": "parallelJoin", "label": "Join All",
                "joinData": {
                  "policy": "all",
                  "mergeStrategy": "last_writer_wins",
                  "variableMergeStrategies": {
                    "approvers": "append",
                    "score": "sum"
                  }
                }
              }
            },
            { "id": "end-1", "type": "end", "position": { "x": 560, "y": 0 }, "data": { "nodeType": "end", "label": "Completed" } }
          ],
          "edges": [
            { "id": "e0", "source": "start", "target": "split-1" },
            { "id": "branch-a", "source": "split-1", "target": "appr-a" },
            { "id": "branch-b", "source": "split-1", "target": "appr-b" },
            { "id": "e-a-join", "source": "appr-a", "target": "join-1", "sourceHandle": "approve" },
            { "id": "e-b-join", "source": "appr-b", "target": "join-1", "sourceHandle": "approve" },
            { "id": "e-join-end", "source": "join-1", "target": "end-1" }
          ]
        }
        """;

        (await PublishDefinitionAsync(client, wfKey, graphJson)).StatusCode.Should().Be(HttpStatusCode.Created);
        var started = await StartInstanceAsync(client, wfKey);

        // Verify ViewFlow exposes 2 active branch tokens & Join state (0/2)
        var initialFlow = await client.GetFromJsonAsync<ViewFlowDto>($"/api/instances/{started.InstanceId}/flow");
        initialFlow.Should().NotBeNull();
        initialFlow!.Tokens.Should().HaveCount(2);
        initialFlow.ActiveTokens.Should().HaveCount(2);
        initialFlow.JoinStates.Should().ContainSingle(j => j.ExpectedCount == 2 && j.ArrivedCount == 0 && !j.JoinFired);

        // Fetch tasks for both branches from DB
        List<(string TaskId, string NodeId)> tasks;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            tasks = await db.WorkflowTasks
                .Where(t => t.InstanceId == started.InstanceId && t.Status == TaskStatus.Pending)
                .Select(t => ValueTuple.Create(t.TaskId, t.NodeId))
                .ToListAsync();
        }
        tasks.Should().HaveCount(2);
        var taskA = tasks.Single(t => t.NodeId == "appr-a").TaskId;
        var taskB = tasks.Single(t => t.NodeId == "appr-b").TaskId;

        // Complete Branch B first to prove deterministic ordering by BranchId (branch-a before branch-b)
        var completeBRes = await client.PostAsJsonAsync($"/api/tasks/{taskB}/complete", new
        {
            outcome = "approve",
            formPatch = new Dictionary<string, object?>
            {
                ["approvers"] = "Bob",
                ["score"] = 25,
                ["branchBOnly"] = "from-b"
            }
        });
        completeBRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Instance must still be waiting for Branch A
        var midFlow = await client.GetFromJsonAsync<ViewFlowDto>($"/api/instances/{started.InstanceId}/flow");
        midFlow!.Instance.Status.Should().Be("Running");
        midFlow.JoinStates.Should().ContainSingle(j => j.ArrivedCount == 1 && j.ExpectedCount == 2 && !j.JoinFired);

        // Complete Branch A -> Join fires & merges variables deterministically
        var completeARes = await client.PostAsJsonAsync($"/api/tasks/{taskA}/complete", new
        {
            outcome = "approve",
            formPatch = new Dictionary<string, object?>
            {
                ["approvers"] = "Alice",
                ["score"] = 10,
                ["branchAOnly"] = "from-a"
            }
        });
        completeARes.StatusCode.Should().Be(HttpStatusCode.OK);

        var finalFlow = await client.GetFromJsonAsync<ViewFlowDto>($"/api/instances/{started.InstanceId}/flow");
        finalFlow!.Instance.Status.Should().Be("Completed");
        finalFlow.JoinStates.Should().ContainSingle(j => j.ArrivedCount == 2 && j.ExpectedCount == 2 && j.JoinFired);
        finalFlow.Variables["branchAOnly"]!.ToString().Should().Be("from-a");
        finalFlow.Variables["branchBOnly"]!.ToString().Should().Be("from-b");
        decimal.Parse(finalFlow.Variables["score"]!.ToString()!).Should().Be(35m);

        var approversJson = (JsonElement)finalFlow.Variables["approvers"]!;
        approversJson.EnumerateArray().Select(e => e.GetString()).Should().ContainInOrder("Alice", "Bob");
    }

    [Fact]
    public async Task ParallelSplitJoinAny_FiresOnceOnFirstBranchAndCancelsRemainingTasks()
    {
        var client = await CreateAuthenticatedClientAsync();
        string wfKey = $"par-any-{Guid.NewGuid():N}"[..20];
        string assignee = "adel.sebti@corp.internal";

        string graphJson = $$"""
        {
          "id": "{{wfKey}}",
          "name": "Parallel Any Cancel Remaining",
          "version": "1.0",
          "nodes": [
            { "id": "start", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            { "id": "split-1", "type": "parallelSplit", "position": { "x": 120, "y": 0 }, "data": { "nodeType": "parallelSplit", "label": "Split", "splitData": { "mode": "all" } } },
            {
              "id": "appr-1", "type": "approval", "position": { "x": 260, "y": -80 },
              "data": {
                "nodeType": "approval", "label": "Approver 1",
                "approvalData": {
                  "assignmentType": "user", "assignee": "{{assignee}}",
                  "outcomes": [{ "id": "approve", "label": "Approve", "color": "#059669" }]
                }
              }
            },
            {
              "id": "appr-2", "type": "approval", "position": { "x": 260, "y": 80 },
              "data": {
                "nodeType": "approval", "label": "Approver 2",
                "approvalData": {
                  "assignmentType": "user", "assignee": "{{assignee}}",
                  "outcomes": [{ "id": "approve", "label": "Approve", "color": "#059669" }]
                }
              }
            },
            {
              "id": "join-1", "type": "parallelJoin", "position": { "x": 420, "y": 0 },
              "data": {
                "nodeType": "parallelJoin", "label": "Join Any",
                "joinData": { "policy": "any", "onRemaining": "cancel" }
              }
            },
            { "id": "end-1", "type": "end", "position": { "x": 560, "y": 0 }, "data": { "nodeType": "end", "label": "Done" } }
          ],
          "edges": [
            { "id": "e0", "source": "start", "target": "split-1" },
            { "id": "b1", "source": "split-1", "target": "appr-1" },
            { "id": "b2", "source": "split-1", "target": "appr-2" },
            { "id": "e1", "source": "appr-1", "target": "join-1", "sourceHandle": "approve" },
            { "id": "e2", "source": "appr-2", "target": "join-1", "sourceHandle": "approve" },
            { "id": "e3", "source": "join-1", "target": "end-1" }
          ]
        }
        """;

        (await PublishDefinitionAsync(client, wfKey, graphJson)).StatusCode.Should().Be(HttpStatusCode.Created);
        var started = await StartInstanceAsync(client, wfKey);

        string task1Id;
        string task2Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var pending = await db.WorkflowTasks.Where(t => t.InstanceId == started.InstanceId).ToListAsync();
            pending.Should().HaveCount(2);
            task1Id = pending.Single(t => t.NodeId == "appr-1").TaskId;
            task2Id = pending.Single(t => t.NodeId == "appr-2").TaskId;
        }

        // Complete first branch -> Join any fires immediately and cancels sibling task & token
        var res = await client.PostAsJsonAsync($"/api/tasks/{task1Id}/complete", new { outcome = "approve" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var instance = await db.WorkflowInstances
                .Include(i => i.Tasks)
                .Include(i => i.Tokens)
                .Include(i => i.JoinStates)
                .SingleAsync(i => i.InstanceId == started.InstanceId);

            instance.Status.Should().Be(WorkflowStatus.Completed);
            instance.JoinStates.Should().ContainSingle(j => j.JoinFired && j.ArrivedCount == 1);
            instance.Tasks.Single(t => t.TaskId == task1Id).Status.Should().Be(TaskStatus.Approved);
            instance.Tasks.Single(t => t.TaskId == task2Id).Status.Should().Be(TaskStatus.Cancelled);
            instance.Tokens.Count(t => t.Status == TokenStatus.Completed).Should().Be(1);
            instance.Tokens.Count(t => t.Status == TokenStatus.Cancelled).Should().Be(1);
        }
    }

    [Fact]
    public async Task ParallelSplitJoin_UnderConcurrentBranchCompletion_TriggersDownstreamExactlyOnce()
    {
        var client = await CreateAuthenticatedClientAsync();
        string wfKey = $"par-conc-{Guid.NewGuid():N}"[..20];
        string assignee = "adel.sebti@corp.internal";

        string graphJson = $$"""
        {
          "id": "{{wfKey}}",
          "name": "Concurrent Join Exactly Once",
          "version": "1.0",
          "nodes": [
            { "id": "start", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            { "id": "split-1", "type": "parallelSplit", "position": { "x": 100, "y": 0 }, "data": { "nodeType": "parallelSplit", "label": "Split 3", "splitData": { "mode": "all" } } },
            {
              "id": "appr-1", "type": "approval", "position": { "x": 220, "y": -100 },
              "data": { "nodeType": "approval", "label": "Branch 1", "approvalData": { "assignmentType": "user", "assignee": "{{assignee}}", "outcomes": [{ "id": "approve", "label": "Approve", "color": "#059669" }] } }
            },
            {
              "id": "appr-2", "type": "approval", "position": { "x": 220, "y": 0 },
              "data": { "nodeType": "approval", "label": "Branch 2", "approvalData": { "assignmentType": "user", "assignee": "{{assignee}}", "outcomes": [{ "id": "approve", "label": "Approve", "color": "#059669" }] } }
            },
            {
              "id": "appr-3", "type": "approval", "position": { "x": 220, "y": 100 },
              "data": { "nodeType": "approval", "label": "Branch 3", "approvalData": { "assignmentType": "user", "assignee": "{{assignee}}", "outcomes": [{ "id": "approve", "label": "Approve", "color": "#059669" }] } }
            },
            {
              "id": "join-1", "type": "parallelJoin", "position": { "x": 360, "y": 0 },
              "data": { "nodeType": "parallelJoin", "label": "Join All", "joinData": { "policy": "all" } }
            },
            {
              "id": "appr-downstream", "type": "approval", "position": { "x": 500, "y": 0 },
              "data": { "nodeType": "approval", "label": "Final Sign-Off", "approvalData": { "assignmentType": "user", "assignee": "{{assignee}}", "outcomes": [{ "id": "approve", "label": "Approve", "color": "#059669" }] } }
            },
            { "id": "end-1", "type": "end", "position": { "x": 640, "y": 0 }, "data": { "nodeType": "end", "label": "End" } }
          ],
          "edges": [
            { "id": "e0", "source": "start", "target": "split-1" },
            { "id": "b1", "source": "split-1", "target": "appr-1" },
            { "id": "b2", "source": "split-1", "target": "appr-2" },
            { "id": "b3", "source": "split-1", "target": "appr-3" },
            { "id": "j1", "source": "appr-1", "target": "join-1", "sourceHandle": "approve" },
            { "id": "j2", "source": "appr-2", "target": "join-1", "sourceHandle": "approve" },
            { "id": "j3", "source": "appr-3", "target": "join-1", "sourceHandle": "approve" },
            { "id": "e-down", "source": "join-1", "target": "appr-downstream" },
            { "id": "e-end", "source": "appr-downstream", "target": "end-1", "sourceHandle": "approve" }
          ]
        }
        """;

        (await PublishDefinitionAsync(client, wfKey, graphJson)).StatusCode.Should().Be(HttpStatusCode.Created);
        var started = await StartInstanceAsync(client, wfKey);

        List<string> branchTaskIds;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            branchTaskIds = await db.WorkflowTasks
                .Where(t => t.InstanceId == started.InstanceId && t.Status == TaskStatus.Pending)
                .Select(t => t.TaskId)
                .ToListAsync();
        }
        branchTaskIds.Should().HaveCount(3);

        // Fire all 3 branch completions concurrently using 3 separate HttpClients
        var concurrentClients = await Task.WhenAll(
            CreateAuthenticatedClientAsync(),
            CreateAuthenticatedClientAsync(),
            CreateAuthenticatedClientAsync());

        var responses = await Task.WhenAll(
            branchTaskIds.Select((taskId, idx) =>
                concurrentClients[idx].PostAsJsonAsync($"/api/tasks/{taskId}/complete", new { outcome = "approve" })));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        // Verify JoinState fired exactly once and exactly 1 downstream task was spawned
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var joinState = await db.JoinStates.SingleAsync(j => j.InstanceId == started.InstanceId);
            joinState.ExpectedCount.Should().Be(3);
            joinState.ArrivedCount.Should().Be(3);
            joinState.JoinFired.Should().BeTrue();

            var downstreamTasks = await db.WorkflowTasks
                .Where(t => t.InstanceId == started.InstanceId && t.NodeId == "appr-downstream")
                .ToListAsync();
            downstreamTasks.Should().HaveCount(1);

            var joinFiredEvents = await db.InstanceEvents
                .Where(e => e.InstanceId == started.InstanceId && e.Type == "Parallel.JoinFired")
                .ToListAsync();
            joinFiredEvents.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task ParallelDynamicForEach_SpawnsOneBranchPerCollectionItem()
    {
        var client = await CreateAuthenticatedClientAsync();
        string wfKey = $"par-fe-{Guid.NewGuid():N}"[..20];

        string graphJson = $$$"""
        {
          "id": "{{{wfKey}}}",
          "name": "Dynamic ForEach Parallel",
          "version": "1.0",
          "nodes": [
            { "id": "start", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            {
              "id": "split-fe", "type": "parallelSplit", "position": { "x": 140, "y": 0 },
              "data": {
                "nodeType": "parallelSplit", "label": "ForEach Reviewers",
                "splitData": {
                  "mode": "dynamic_foreach",
                  "collectionExpression": "reviewers",
                  "itemVariable": "reviewer"
                }
              }
            },
            {
              "id": "appr-item", "type": "approval", "position": { "x": 300, "y": 0 },
              "data": {
                "nodeType": "approval", "label": "Review {{reviewer.dept}}",
                "approvalData": {
                  "assignmentType": "user",
                  "assignee": "{{reviewer.email}}",
                  "outcomes": [{ "id": "approve", "label": "Approve", "color": "#059669" }]
                }
              }
            },
            {
              "id": "join-fe", "type": "parallelJoin", "position": { "x": 460, "y": 0 },
              "data": {
                "nodeType": "parallelJoin", "label": "Join All Items",
                "joinData": { "policy": "all", "mergeStrategy": "append" }
              }
            },
            { "id": "end-1", "type": "end", "position": { "x": 600, "y": 0 }, "data": { "nodeType": "end", "label": "End" } }
          ],
          "edges": [
            { "id": "e0", "source": "start", "target": "split-fe" },
            { "id": "e1", "source": "split-fe", "target": "appr-item" },
            { "id": "e2", "source": "appr-item", "target": "join-fe", "sourceHandle": "approve" },
            { "id": "e3", "source": "join-fe", "target": "end-1" }
          ]
        }
        """;

        (await PublishDefinitionAsync(client, wfKey, graphJson)).StatusCode.Should().Be(HttpStatusCode.Created);

        var started = await StartInstanceAsync(client, wfKey, new Dictionary<string, object>
        {
            ["reviewers"] = new[]
            {
                new { email = "adel.sebti@corp.internal", dept = "HR" },
                new { email = "adel.sebti@corp.internal", dept = "IT" },
                new { email = "adel.sebti@corp.internal", dept = "Legal" }
            }
        });

        List<(string TaskId, string Title, string? BranchId)> spawnedTasks;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            var tokens = await db.WorkflowTokens.Where(t => t.InstanceId == started.InstanceId).ToListAsync();
            tokens.Should().HaveCount(3);
            tokens.Select(t => t.BranchId).Should().BeEquivalentTo(new[] { "item-0", "item-1", "item-2" });

            var joinState = await db.JoinStates.SingleAsync(j => j.InstanceId == started.InstanceId);
            joinState.ExpectedCount.Should().Be(3);
            joinState.ArrivedCount.Should().Be(0);

            spawnedTasks = await db.WorkflowTasks
                .Where(t => t.InstanceId == started.InstanceId && t.Status == TaskStatus.Pending)
                .Select(t => ValueTuple.Create(t.TaskId, t.TaskTitle, t.BranchId))
                .ToListAsync();
        }

        spawnedTasks.Should().HaveCount(3);
        spawnedTasks.Select(t => t.Title).Should().BeEquivalentTo(new[] { "Review HR", "Review IT", "Review Legal" });

        // Complete all 3 dynamically spawned tasks
        foreach (var task in spawnedTasks)
        {
            var completeRes = await client.PostAsJsonAsync($"/api/tasks/{task.TaskId}/complete", new { outcome = "approve" });
            completeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var finalFlow = await client.GetFromJsonAsync<ViewFlowDto>($"/api/instances/{started.InstanceId}/flow");
        finalFlow!.Instance.Status.Should().Be("Completed");
        finalFlow.JoinStates.Should().ContainSingle(j => j.ExpectedCount == 3 && j.ArrivedCount == 3 && j.JoinFired);
    }
}
