using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Features.Instances.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Security;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

public class ViewFlowAndEventsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ViewFlowAndEventsTests(TestWebApplicationFactory factory)
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

    private async Task<string> EnsureStandardUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
        var existing = await db.SystemUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (existing == null)
        {
            var user = new SystemUser(
                $"usr-{Guid.NewGuid():N}"[..18],
                "Standard Viewer",
                email,
                "End User",
                "Operations",
                "Active",
                "/avatars/default.svg",
                PasswordHasher.HashPassword(DatabaseInitializer.DefaultSeedPassword));
            db.SystemUsers.Add(user);
            await db.SaveChangesAsync();
        }
        return email;
    }

    private static string BuildGraphJson(string wfKey, string assigneeEmail) => $$"""
    {
      "id": "{{wfKey}}",
      "name": "ViewFlow Test Workflow",
      "version": "1.0",
      "nodes": [
        {
          "id": "trig-start",
          "type": "trigger",
          "position": { "x": 0, "y": 0 },
          "data": {
            "nodeType": "trigger",
            "label": "Start",
            "triggerData": {
              "triggerType": "form",
              "eventName": "onSubmit",
              "variables": [
                { "name": "department", "type": "string", "isSensitive": false },
                { "name": "customSecretCode", "type": "string", "isSensitive": true }
              ]
            }
          }
        },
        {
          "id": "appr-1",
          "type": "approval",
          "position": { "x": 200, "y": 0 },
          "data": {
            "nodeType": "approval",
            "label": "Manager Approval",
            "approvalData": {
              "assignmentType": "user",
              "assignee": "{{assigneeEmail}}",
              "outcomes": [
                { "id": "approve", "label": "Approve", "color": "#059669" },
                { "id": "reject", "label": "Reject", "color": "#DC2626" }
              ],
              "dueDateDays": 2
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
        { "id": "e1", "source": "trig-start", "target": "appr-1" },
        { "id": "e2", "source": "appr-1", "target": "end-1", "sourceHandle": "approve" }
      ]
    }
    """;

    private async Task<string> CreateAndStartInstanceAsync(
        HttpClient client,
        Dictionary<string, object>? triggerVariables = null)
    {
        string wfKey = $"vf-wf-{Guid.NewGuid():N}"[..20];
        string graphJson = BuildGraphJson(wfKey, "adel.sebti@corp.internal");

        var createDefRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "ViewFlow Definition",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Operations"
        });
        createDefRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var startRes = await client.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = "adel.sebti@corp.internal",
            triggerVariables
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startRes.Content.ReadFromJsonAsync<WorkflowInstanceStartedResult>();
        started.Should().NotBeNull();
        return started!.InstanceId;
    }

    [Fact]
    public async Task GetInstanceFlow_ReturnsNodesTimelineAndDefinitionVersion()
    {
        var client = await CreateAuthenticatedClientAsync();
        string instanceId = await CreateAndStartInstanceAsync(client, new Dictionary<string, object>
        {
            ["department"] = "Finance",
            ["amount"] = 1200
        });

        // Trigger a Suspend and Resume so timeline contains ordered InstanceEvents
        var suspendRes = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/suspend", new
        {
            commandId = Guid.NewGuid(),
            reason = "Pausing for ViewFlow inspection"
        });
        suspendRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var resumeRes = await client.PostAsJsonAsync($"/api/instances/{instanceId}/commands/resume", new
        {
            commandId = Guid.NewGuid()
        });
        resumeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Fetch ViewFlow projection
        var flowRes = await client.GetAsync($"/api/instances/{instanceId}/flow");
        flowRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var flow = await flowRes.Content.ReadFromJsonAsync<ViewFlowDto>();
        flow.Should().NotBeNull();
        flow!.Instance.Id.Should().Be(instanceId);
        flow.Instance.Status.Should().Be("Running");
        flow.Instance.DefinitionId.Should().BeGreaterThan(0);
        flow.Instance.DefinitionVersion.Should().Be(1);
        flow.DefinitionJson.Should().Contain("trig-start").And.Contain("appr-1");

        // NodeExecution projection populated in order
        flow.Nodes.Should().HaveCountGreaterThanOrEqualTo(2);
        flow.Nodes.Should().Contain(n => n.NodeId == "trig-start" && n.Status == "Completed" && n.Attempts == 1);
        flow.Nodes.Should().Contain(n => n.NodeId == "appr-1" && n.Status == "Waiting" && n.Attempts == 1);

        flow.ActiveNodes.Should().Contain("appr-1");
        flow.ActiveTokens.Should().Contain("appr-1");

        // Timeline ordered by Seq
        flow.Timeline.Should().HaveCount(2);
        flow.Timeline[0].Seq.Should().Be(1);
        flow.Timeline[0].Type.Should().Be("Instance.Suspended");
        flow.Timeline[1].Seq.Should().Be(2);
        flow.Timeline[1].Type.Should().Be("Instance.Resumed");
    }

    [Fact]
    public async Task GetInstanceFlow_MasksSensitiveVariablesForStandardUser_AndRevealsForAdmin()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string instanceId = await CreateAndStartInstanceAsync(adminClient, new Dictionary<string, object>
        {
            ["department"] = "Engineering",
            ["employeeSalary"] = 145000,
            ["bankIban"] = "FR7630006000011234567890189",
            ["apiToken"] = "tok_live_998877",
            ["userPassword"] = "Sup3rS3cret!",
            ["customSecretCode"] = "ALPHA-999"
        });

        // 1. Standard user (End User) -> sensitive variables MUST be masked as "***"
        string standardEmail = await EnsureStandardUserAsync("vf.standard@corp.internal");
        var standardClient = await CreateAuthenticatedClientAsync(standardEmail);

        var stdFlowRes = await standardClient.GetAsync($"/api/instances/{instanceId}/flow");
        stdFlowRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var stdFlow = await stdFlowRes.Content.ReadFromJsonAsync<ViewFlowDto>();
        stdFlow.Should().NotBeNull();

        stdFlow!.Variables["department"]!.ToString().Should().Be("Engineering");
        stdFlow.Variables["employeeSalary"]!.ToString().Should().Be("***");
        stdFlow.Variables["bankIban"]!.ToString().Should().Be("***");
        stdFlow.Variables["apiToken"]!.ToString().Should().Be("***");
        stdFlow.Variables["userPassword"]!.ToString().Should().Be("***");
        stdFlow.Variables["customSecretCode"]!.ToString().Should().Be("***");

        // 2. Admin user (BPM Administrator) -> sensitive variables MUST be revealed
        var adminFlowRes = await adminClient.GetAsync($"/api/instances/{instanceId}/flow");
        adminFlowRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminFlow = await adminFlowRes.Content.ReadFromJsonAsync<ViewFlowDto>();
        adminFlow.Should().NotBeNull();

        adminFlow!.Variables["department"]!.ToString().Should().Be("Engineering");
        adminFlow.Variables["employeeSalary"]!.ToString().Should().Be("145000");
        adminFlow.Variables["bankIban"]!.ToString().Should().Be("FR7630006000011234567890189");
        adminFlow.Variables["apiToken"]!.ToString().Should().Be("tok_live_998877");
        adminFlow.Variables["userPassword"]!.ToString().Should().Be("Sup3rS3cret!");
        adminFlow.Variables["customSecretCode"]!.ToString().Should().Be("ALPHA-999");
    }

    [Fact]
    public async Task GetInstanceEvents_SupportsCursorPaginationWithAfterSeqAndLimit()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string instanceId = await CreateAndStartInstanceAsync(adminClient);

        // Seed 5 ordered events (including one with a sensitive field in PayloadJson)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowEngineDbContext>();
            for (int seq = 1; seq <= 5; seq++)
            {
                string payload = seq == 3
                    ? JsonSerializer.Serialize(new { step = seq, apiSecret = "secret-xyz", note = "visible" })
                    : JsonSerializer.Serialize(new { step = seq, note = $"event-{seq}" });

                db.InstanceEvents.Add(new InstanceEvent(
                    instanceId,
                    seq,
                    $"Custom.Event{seq}",
                    "appr-1",
                    "adel.sebti@corp.internal",
                    "user",
                    payload));
            }
            await db.SaveChangesAsync();
        }

        // Page 1: afterSeq=0, limit=2 -> Seq 1, 2 (hasMore=true, nextSeq=2)
        var page1Res = await adminClient.GetAsync($"/api/instances/{instanceId}/events?afterSeq=0&limit=2");
        page1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await page1Res.Content.ReadFromJsonAsync<InstanceEventsPageDto>();
        page1.Should().NotBeNull();
        page1!.Events.Select(e => e.Seq).Should().Equal(1, 2);
        page1.HasMore.Should().BeTrue();
        page1.NextSeq.Should().Be(2);

        // Page 2 as Standard User: afterSeq=2, limit=2 -> Seq 3, 4 (hasMore=true, sensitive payload masked)
        string standardEmail = await EnsureStandardUserAsync("vf.events.std@corp.internal");
        var stdClient = await CreateAuthenticatedClientAsync(standardEmail);

        var page2Res = await stdClient.GetAsync($"/api/instances/{instanceId}/events?afterSeq=2&limit=2");
        page2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await page2Res.Content.ReadFromJsonAsync<InstanceEventsPageDto>();
        page2.Should().NotBeNull();
        page2!.Events.Select(e => e.Seq).Should().Equal(3, 4);
        page2.HasMore.Should().BeTrue();
        page2.NextSeq.Should().Be(4);
        page2.Events[0].PayloadJson.Should().Contain("***").And.NotContain("secret-xyz");

        // Page 3: afterSeq=4, limit=2 -> Seq 5 (hasMore=false, nextSeq=5)
        var page3Res = await adminClient.GetAsync($"/api/instances/{instanceId}/events?afterSeq=4&limit=2");
        page3Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page3 = await page3Res.Content.ReadFromJsonAsync<InstanceEventsPageDto>();
        page3.Should().NotBeNull();
        page3!.Events.Select(e => e.Seq).Should().Equal(5);
        page3.HasMore.Should().BeFalse();
        page3.NextSeq.Should().Be(5);
    }

    [Fact]
    public async Task GetInstanceFlow_WhenInstanceNotFound_Returns404NotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var flowRes = await client.GetAsync("/api/instances/non-existent-inst-999/flow");
        flowRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var eventsRes = await client.GetAsync("/api/instances/non-existent-inst-999/events?afterSeq=0&limit=10");
        eventsRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
