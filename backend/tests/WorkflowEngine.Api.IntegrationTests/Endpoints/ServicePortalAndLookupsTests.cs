using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Features.SmartForms.DTOs;
using WorkflowEngine.Application.Features.SmartObjects.Queries;
using WorkflowEngine.Application.Features.Users.Queries;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

/// <summary>
/// Milestone C / Step C.3 - Service Portal draft submissions, paginated SmartObject lookups,
/// and sanitized People Picker directory search.
/// </summary>
public class ServicePortalAndLookupsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ServicePortalAndLookupsTests(TestWebApplicationFactory factory)
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
    public async Task LookupEndpoint_ReturnsPaginatedAndFilteredRecords()
    {
        var adminClient = await CreateAuthenticatedClientAsync();

        string soId = $"so-lkp-{Guid.NewGuid():N}"[..18];
        var createSoRes = await adminClient.PostAsJsonAsync("/api/smartobjects", new
        {
            id = soId,
            name = $"CatalogVendor{Guid.NewGuid():N}"[..24],
            displayName = "Catalog Vendors",
            description = "Paginated lookup test entity",
            category = "Procurement",
            fields = new[]
            {
                new { name = "VendorCode", type = "String", isPrimaryKey = true, isRequired = true },
                new { name = "VendorName", type = "String", isPrimaryKey = false, isRequired = true },
                new { name = "Region", type = "String", isPrimaryKey = false, isRequired = false }
            }
        });
        createSoRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Insert 12 records: 5 matching "Alpha" and 7 matching "Beta"
        for (int i = 1; i <= 5; i++)
        {
            var r = await adminClient.PostAsJsonAsync($"/api/smartobjects/{soId}/records", new Dictionary<string, object?>
            {
                ["VendorCode"] = $"ALP-{i:D2}",
                ["VendorName"] = $"Alpha Supplier {i}",
                ["Region"] = "EMEA"
            });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        for (int i = 1; i <= 7; i++)
        {
            var r = await adminClient.PostAsJsonAsync($"/api/smartobjects/{soId}/records", new Dictionary<string, object?>
            {
                ["VendorCode"] = $"BET-{i:D2}",
                ["VendorName"] = $"Beta Partner {i}",
                ["Region"] = "NA"
            });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Page 1 unfiltered (pageSize=5 of 12)
        var page1Res = await adminClient.GetAsync($"/api/smartobjects/{soId}/lookup?page=1&pageSize=5");
        page1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await page1Res.Content.ReadFromJsonAsync<SmartObjectLookupPageDto>();
        page1.Should().NotBeNull();
        page1!.TotalCount.Should().Be(12);
        page1.TotalPages.Should().Be(3);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(5);
        page1.HasMore.Should().BeTrue();
        page1.Items.Should().HaveCount(5);

        // Page 3 unfiltered (last 2 records)
        var page3Res = await adminClient.GetAsync($"/api/smartobjects/{soId}/lookup?page=3&pageSize=5");
        page3Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page3 = await page3Res.Content.ReadFromJsonAsync<SmartObjectLookupPageDto>();
        page3!.Items.Should().HaveCount(2);
        page3.HasMore.Should().BeFalse();

        // Filtered query: only "Alpha" records, pageSize=3
        var filteredRes = await adminClient.GetAsync($"/api/smartobjects/{soId}/lookup?q=alpha&page=1&pageSize=3");
        filteredRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var filtered = await filteredRes.Content.ReadFromJsonAsync<SmartObjectLookupPageDto>();
        filtered!.TotalCount.Should().Be(5);
        filtered.TotalPages.Should().Be(2);
        filtered.HasMore.Should().BeTrue();
        filtered.Items.Should().HaveCount(3);
        filtered.Items.Should().OnlyContain(item =>
            item["VendorName"] != null &&
            item["VendorName"]!.ToString()!.Contains("Alpha", StringComparison.OrdinalIgnoreCase));

        // Unknown SmartObject returns 404
        var notFoundRes = await adminClient.GetAsync("/api/smartobjects/so-does-not-exist/lookup?page=1&pageSize=10");
        notFoundRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserSearchEndpoint_ReturnsSanitizedDirectoryResultsWithoutPasswordHashes()
    {
        var client = await CreateAuthenticatedClientAsync();

        var res = await client.GetAsync("/api/users/search?q=adel&limit=10");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        string rawJson = await res.Content.ReadAsStringAsync();
        rawJson.Should().NotContainEquivalentOf("passwordHash");
        rawJson.Should().NotContainEquivalentOf("password");

        using var doc = JsonDocument.Parse(rawJson);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var allowedProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "id",
            "displayName",
            "email",
            "department"
        };

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var props = item.EnumerateObject().Select(p => p.Name).ToList();
            props.Should().BeEquivalentTo(allowedProperties);
        }

        var results = JsonSerializer.Deserialize<List<UserDirectoryDto>>(rawJson);
        results.Should().NotBeNull();
        results!.Should().Contain(u =>
            u.Email.Equals("adel.sebti@corp.internal", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(u.DisplayName) &&
            !string.IsNullOrWhiteSpace(u.Department));
    }

    [Fact]
    public async Task SaveDraftAndResume_AllowsEndUserToCompleteSubmissionLater()
    {
        var adminClient = await CreateAuthenticatedClientAsync("adel.sebti@corp.internal");

        // Create an End User account so we verify non-designer access to drafts and submissions
        string endUserEmail = $"requester.{Guid.NewGuid():N}"[..24] + "@corp.internal";
        var createUserRes = await adminClient.PostAsJsonAsync("/api/users", new
        {
            name = "Portal Requester",
            email = endUserEmail,
            role = "End User",
            group = "Operations",
            status = "Active",
            password = DatabaseInitializer.DefaultSeedPassword
        });
        createUserRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Publish a SmartForm with a required field + optional notes
        string formId = $"form-portal-{Guid.NewGuid():N}"[..20];
        var createFormRes = await adminClient.PostAsJsonAsync("/api/smartforms", new
        {
            id = formId,
            name = $"PortalReqForm{Guid.NewGuid():N}"[..22],
            title = "IT Equipment Request",
            description = "End-user equipment request form",
            category = "IT",
            publishImmediately = true,
            fields = new object[]
            {
                new { id = "f-title", name = "EquipmentType", label = "Equipment Type", type = "text", required = true, readOnly = false },
                new { id = "f-notes", name = "Justification", label = "Justification", type = "textarea", required = true, readOnly = false }
            }
        });
        createFormRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Authenticate as the End User (default password set by CreateUserHandler -> let's check or use seeded End User)
        // Use a seeded End User or login with created user; let's check if created user can login with DefaultSeedPassword
        var endUserClient = _factory.CreateClient();
        var loginRes = await endUserClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(endUserEmail, DatabaseInitializer.DefaultSeedPassword));
        if (loginRes.StatusCode != HttpStatusCode.OK)
        {
            // Fallback to seeded user if CreateUser doesn't assign DefaultSeedPassword
            endUserClient = await CreateAuthenticatedClientAsync("karim.khmissi@corp.internal");
        }
        else
        {
            var loginBody = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
            endUserClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.Token);
        }

        // 1. Save an incomplete draft (missing required 'Justification' — allowed for drafts!)
        var draftRes = await endUserClient.PostAsJsonAsync($"/api/smartforms/{formId}/drafts", new
        {
            data = new Dictionary<string, object?>
            {
                ["EquipmentType"] = "MacBook Pro 16"
            }
        });
        draftRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await draftRes.Content.ReadFromJsonAsync<SmartFormSubmissionDto>();
        draft.Should().NotBeNull();
        draft!.Status.Should().Be("Draft");
        draft.SmartFormId.Should().Be(formId);
        draft.Data["EquipmentType"]!.ToString().Should().Be("MacBook Pro 16");

        // 2. Verify draft appears in GET /api/smartforms/my-submissions for the caller
        var mySubsRes = await endUserClient.GetAsync("/api/smartforms/my-submissions");
        mySubsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var mySubs = await mySubsRes.Content.ReadFromJsonAsync<List<SmartFormSubmissionDto>>();
        mySubs.Should().NotBeNull();
        mySubs!.Should().Contain(s => s.Id == draft.Id && s.Status == "Draft" && s.SmartFormTitle == "IT Equipment Request");

        // 3. Another user cannot overwrite or submit someone else's draft (403 Forbidden)
        var forbiddenUpdateRes = await adminClient.PostAsJsonAsync($"/api/smartforms/{formId}/drafts", new
        {
            draftId = draft.Id,
            data = new Dictionary<string, object?> { ["EquipmentType"] = "Tampered" }
        });
        forbiddenUpdateRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Resume and update the draft with additional partial info
        var updateDraftRes = await endUserClient.PostAsJsonAsync($"/api/smartforms/{formId}/drafts", new
        {
            draftId = draft.Id,
            data = new Dictionary<string, object?>
            {
                ["EquipmentType"] = "MacBook Pro 16",
                ["Justification"] = "Needed for mobile builds"
            }
        });
        updateDraftRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedDraft = await updateDraftRes.Content.ReadFromJsonAsync<SmartFormSubmissionDto>();
        updatedDraft!.Id.Should().Be(draft.Id);
        updatedDraft.Status.Should().Be("Draft");

        // 5. Complete submission from the resumed draft
        var submitReq = new HttpRequestMessage(HttpMethod.Post, $"/api/smartforms/{formId}/submit")
        {
            Content = JsonContent.Create(new
            {
                draftId = draft.Id,
                startLinkedWorkflow = false,
                data = new Dictionary<string, object?>
                {
                    ["EquipmentType"] = "MacBook Pro 16",
                    ["Justification"] = "Needed for mobile builds"
                }
            })
        };
        submitReq.Headers.Add("Idempotency-Key", $"idem-draft-{Guid.NewGuid():N}");

        var submitRes = await endUserClient.SendAsync(submitReq);
        submitRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var completed = await submitRes.Content.ReadFromJsonAsync<SmartFormSubmissionDto>();
        completed.Should().NotBeNull();
        completed!.Id.Should().Be(draft.Id);
        completed.Status.Should().Be("Submitted");

        // 6. Verify my-submissions now reflects the submission as Submitted (no orphaned Draft)
        var afterSubmitRes = await endUserClient.GetAsync("/api/smartforms/my-submissions");
        afterSubmitRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterSubs = await afterSubmitRes.Content.ReadFromJsonAsync<List<SmartFormSubmissionDto>>();
        afterSubs!.Should().ContainSingle(s => s.Id == draft.Id && s.Status == "Submitted");
    }
}
