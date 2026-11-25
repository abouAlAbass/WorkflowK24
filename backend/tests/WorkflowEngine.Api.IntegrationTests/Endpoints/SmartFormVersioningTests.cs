using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Features.SmartForms.DTOs;
using WorkflowEngine.Application.Features.Tasks.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

/// <summary>
/// Milestone A / Step A.3 - Immutable SmartFormVersion lifecycle (Draft -> Published -> Archived),
/// submission pinning, and task formVersionId isolation.
/// </summary>
public class SmartFormVersioningTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SmartFormVersioningTests(TestWebApplicationFactory factory)
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

    private static string ApprovalGraphJson(string wfKey, string assignee, string formId) => $$"""
    {
      "id": "{{wfKey}}",
      "name": "Versioned Form Approval Workflow",
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
            "label": "Finance Review Task",
            "approvalData": {
              "assignmentType": "user",
              "assignee": "{{assignee}}",
              "outcomes": [
                { "id": "approve", "label": "Approve", "color": "#059669" },
                { "id": "reject", "label": "Reject", "color": "#DC2626" }
              ],
              "dueDateDays": 2,
              "formId": "{{formId}}",
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

    [Fact]
    public async Task UpdatePublishedSmartForm_CreatesNewDraftVersionWithoutMutatingPublishedVersion()
    {
        var client = await CreateAuthenticatedClientAsync();
        string formId = $"form-immut-{Guid.NewGuid():N}"[..20];

        // 1. Create form v1 (Draft) and publish it
        var createRes = await client.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "ImmutableTestForm",
            title = "Immutable Form",
            category = "Finance",
            fields = new object[]
            {
                new { id = "f1", name = "VendorName", label = "Vendor Name", type = "text", required = true, readOnly = false }
            }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<SmartFormDto>();
        created!.LatestVersionNumber.Should().Be(1);
        created.LatestVersionStatus.Should().Be("Draft");
        created.ActivePublishedVersionId.Should().BeNull();

        var publishRes = await client.PostAsync($"/api/smartforms/{formId}/publish", null);
        publishRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var published = await publishRes.Content.ReadFromJsonAsync<SmartFormDto>();
        published!.ActivePublishedVersionId.Should().NotBeNullOrWhiteSpace();
        string v1Id = published.ActivePublishedVersionId!;
        published.LatestVersionNumber.Should().Be(1);
        published.LatestVersionStatus.Should().Be("Published");

        // 2. Edit the published form via PUT -> automatically creates v2 Draft without mutating v1
        var updateRes = await client.PutAsJsonAsync($"/api/smartforms/{formId}", new
        {
            id = formId,
            title = "Immutable Form (Edited Draft)",
            fields = new object[]
            {
                new { id = "f1", name = "VendorName", label = "Vendor Name", type = "text", required = true, readOnly = false },
                new { id = "f2", name = "CostCenter", label = "Cost Center", type = "text", required = true, readOnly = false }
            }
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<SmartFormDto>();
        updated!.ActivePublishedVersionId.Should().Be(v1Id);
        updated.LatestVersionNumber.Should().Be(2);
        updated.LatestVersionStatus.Should().Be("Draft");
        updated.HasUnpublishedDraft.Should().BeTrue();
        // Backward-compat: Fields on definition still reflects active Published v1
        updated.Fields.Should().ContainSingle(f => f.Name == "VendorName");
        updated.DraftFields.Should().HaveCount(2);

        // 3. Verify GET /versions lists v2 (Draft) and v1 (Published)
        var versions = await client.GetFromJsonAsync<List<SmartFormVersionSummaryDto>>($"/api/smartforms/{formId}/versions");
        versions.Should().NotBeNull();
        versions!.Should().HaveCount(2);
        versions[0].VersionNumber.Should().Be(2);
        versions[0].Status.Should().Be("Draft");
        versions[1].VersionNumber.Should().Be(1);
        versions[1].Status.Should().Be("Published");
        versions[1].Id.Should().Be(v1Id);

        // 4. Verify v1 immutable snapshot still has ONLY VendorName (never mutated)
        var v1Detail = await client.GetFromJsonAsync<SmartFormVersionDetailDto>($"/api/smartforms/{formId}/versions/{v1Id}");
        v1Detail.Should().NotBeNull();
        v1Detail!.Status.Should().Be("Published");
        v1Detail.Fields.Should().ContainSingle(f => f.Name == "VendorName");

        // 5. Verify v2 Draft snapshot has both fields
        var v2Detail = await client.GetFromJsonAsync<SmartFormVersionDetailDto>($"/api/smartforms/{formId}/versions/{versions[0].Id}");
        v2Detail.Should().NotBeNull();
        v2Detail!.Status.Should().Be("Draft");
        v2Detail.Fields.Should().HaveCount(2);

        // 6. Verify strict domain immutability rule throws SmartFormVersionImmutableException
        var domainPublished = new SmartFormVersion(formId, 1, "[]", SmartFormVersionStatus.Published, "adel.sebti@corp.internal");
        Action mutateAttempt = () => domainPublished.UpdateFields("[{\"name\":\"Hack\"}]");
        mutateAttempt.Should().Throw<SmartFormVersionImmutableException>()
            .WithMessage("*FORM_VERSION_IMMUTABLE*");
    }

    [Fact]
    public async Task PublishSmartForm_ArchivesPreviousPublishedVersionAndActivatesNewVersion()
    {
        var client = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string formId = $"form-pub-{Guid.NewGuid():N}"[..20];

        // Create and publish v1
        var createRes = await client.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "PublishArchiveForm",
            title = "Publish Archive Form",
            category = "HR",
            fields = new object[]
            {
                new { id = "f1", name = "EmployeeName", label = "Employee Name", type = "text", required = true, readOnly = false }
            }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var pubV1Res = await client.PostAsync($"/api/smartforms/{formId}/publish", null);
        pubV1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var pubV1 = await pubV1Res.Content.ReadFromJsonAsync<SmartFormDto>();
        string v1Id = pubV1!.ActivePublishedVersionId!;

        // Create v2 Draft
        var editRes = await client.PutAsJsonAsync($"/api/smartforms/{formId}", new
        {
            id = formId,
            fields = new object[]
            {
                new { id = "f1", name = "EmployeeName", label = "Employee Name", type = "text", required = true, readOnly = false },
                new { id = "f2", name = "Department", label = "Department", type = "text", required = false, readOnly = false }
            }
        });
        editRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Publish v2 -> v1 becomes Archived, v2 becomes Published and ActivePublishedVersionId
        var pubV2Res = await client.PostAsync($"/api/smartforms/{formId}/publish", null);
        pubV2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var pubV2 = await pubV2Res.Content.ReadFromJsonAsync<SmartFormDto>();
        pubV2!.ActivePublishedVersionId.Should().NotBeNullOrWhiteSpace();
        pubV2.ActivePublishedVersionId.Should().NotBe(v1Id);
        pubV2.LatestVersionNumber.Should().Be(2);
        pubV2.LatestVersionStatus.Should().Be("Published");
        pubV2.HasUnpublishedDraft.Should().BeFalse();

        var versions = await client.GetFromJsonAsync<List<SmartFormVersionSummaryDto>>($"/api/smartforms/{formId}/versions");
        versions.Should().NotBeNull();
        versions!.Should().HaveCount(2);

        var v2 = versions.Single(v => v.VersionNumber == 2);
        var v1 = versions.Single(v => v.VersionNumber == 1);

        v2.Id.Should().Be(pubV2.ActivePublishedVersionId);
        v2.Status.Should().Be("Published");
        v2.PublishedBy.Should().Be("adel.sebti@corp.internal");
        v2.PublishedAt.Should().NotBeNull();

        v1.Id.Should().Be(v1Id);
        v1.Status.Should().Be("Archived");
    }

    [Fact]
    public async Task SubmitSmartForm_PinsActivePublishedVersionId_AndSubsequentDraftEditsDoNotAffectExistingSubmissionOrTask()
    {
        var client = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");
        string formId = $"form-pin-{Guid.NewGuid():N}"[..20];
        string wfKey = $"wf-pin-{Guid.NewGuid():N}"[..22];

        // 1. Register approval workflow bound to formId
        var defRes = await client.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "Pinned Form Workflow",
            graphJson = ApprovalGraphJson(wfKey, "karim.khmissi@corp.internal", formId),
            createdBy = "adel.sebti@corp.internal",
            category = "Finance"
        });
        defRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Create SmartForm v1 and publish it
        var createFormRes = await client.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "PinnedForm",
            title = "Pinned Procurement Form",
            category = "Finance",
            linkedWorkflowKey = wfKey,
            fields = new object[]
            {
                new { id = "f1", name = "VendorName", label = "Vendor Name", type = "text", required = true, readOnly = false }
            }
        });
        createFormRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var pubV1Res = await client.PostAsync($"/api/smartforms/{formId}/publish", null);
        pubV1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var pubV1 = await pubV1Res.Content.ReadFromJsonAsync<SmartFormDto>();
        string v1Id = pubV1!.ActivePublishedVersionId!;

        // 3. Submit against v1 -> starts workflow instance & creates approval task
        var submitRes = await client.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?> { ["VendorName"] = "Acme Corp" },
            startLinkedWorkflow = true
        });
        submitRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var submission = await submitRes.Content.ReadFromJsonAsync<SmartFormSubmissionDto>();
        submission.Should().NotBeNull();
        submission!.SmartFormVersionId.Should().Be(v1Id);
        submission.LinkedInstanceId.Should().NotBeNullOrWhiteSpace();

        // 4. Subsequent Draft edit AND publish of v2 with a breaking new required field
        var editRes = await client.PutAsJsonAsync($"/api/smartforms/{formId}", new
        {
            id = formId,
            fields = new object[]
            {
                new { id = "f1", name = "VendorName", label = "Vendor Name", type = "text", required = true, readOnly = false },
                new { id = "f2", name = "MandatoryAuditCode", label = "Audit Code", type = "text", required = true, readOnly = false }
            }
        });
        editRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var pubV2Res = await client.PostAsync($"/api/smartforms/{formId}/publish", null);
        pubV2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var pubV2 = await pubV2Res.Content.ReadFromJsonAsync<SmartFormDto>();
        pubV2!.ActivePublishedVersionId.Should().NotBe(v1Id);

        // 5. Verify historical submission is still pinned to v1
        var storedSubmissions = await client.GetFromJsonAsync<List<SmartFormSubmissionDto>>($"/api/smartforms/{formId}/submissions");
        storedSubmissions.Should().ContainSingle();
        storedSubmissions![0].SmartFormVersionId.Should().Be(v1Id);

        // 6. Verify existing task exposes FormVersionId == v1Id and v1 schema is untouched
        var tasks = await client.GetFromJsonAsync<List<WorkflowTaskDto>>($"/api/tasks/instance/{submission.LinkedInstanceId}");
        tasks.Should().ContainSingle();
        tasks![0].FormId.Should().Be(formId);
        tasks[0].FormVersionId.Should().Be(v1Id);

        var pinnedSchema = await client.GetFromJsonAsync<SmartFormVersionDetailDto>($"/api/smartforms/{formId}/versions/{tasks[0].FormVersionId}");
        pinnedSchema.Should().NotBeNull();
        pinnedSchema!.VersionNumber.Should().Be(1);
        pinnedSchema.Fields.Should().ContainSingle(f => f.Name == "VendorName");
    }

    [Fact]
    public async Task SubmitSmartForm_WhenOnlyDraftExists_Returns422UnprocessableEntity()
    {
        var client = await CreateAuthenticatedClientAsync();
        string formId = $"form-drf-{Guid.NewGuid():N}"[..20];

        // Create form without publishing (default status = Draft)
        var createRes = await client.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = "DraftOnlyForm",
            title = "Draft Only Form",
            category = "General",
            fields = new object[]
            {
                new { id = "f1", name = "Notes", label = "Notes", type = "text", required = true, readOnly = false }
            }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<SmartFormDto>();
        created!.LatestVersionStatus.Should().Be("Draft");
        created.ActivePublishedVersionId.Should().BeNull();

        // Attempting to submit against a Draft-only form must fail with 422 Unprocessable Entity
        var submitRes = await client.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?> { ["Notes"] = "Should fail" },
            startLinkedWorkflow = false
        });
        submitRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await submitRes.Content.ReadAsStringAsync()).Should().Contain("FORM_NOT_PUBLISHED");
    }
}
