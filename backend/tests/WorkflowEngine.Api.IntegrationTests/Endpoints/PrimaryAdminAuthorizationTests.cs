using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Features.Users.DTOs;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

public class PrimaryAdminAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PrimaryAdminAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> LoginAsync(string email, string password = DatabaseInitializer.DefaultSeedPassword)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    [Theory]
    [InlineData("karim.khmissi@corp.internal")]   // Process Manager
    [InlineData("k.khmissi@enterprise.internal")] // Workflow Designer
    public async Task NonPrincipalUsers_CannotCreateOrDeleteWorkflows(string email)
    {
        var client = await LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/definitions", new { workflowKey = "x" });
        var delete = await client.DeleteAsync("/api/definitions/some-key");

        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PrimaryAdmin_CanCreateUser_WhoCanThenLogin_ButCannotCreateWorkflow()
    {
        var admin = await LoginAsync("adel.sebti@corp.internal");
        string email = $"new.user.{Guid.NewGuid():N}@enterprise.internal";

        var create = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "New User", email, role = "End User", group = "General", status = "Active",
            password = "StrongPass123!"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var newUserClient = await LoginAsync(email, "StrongPass123!");
        (await newUserClient.PostAsJsonAsync("/api/definitions", new { workflowKey = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await newUserClient.PostAsJsonAsync("/api/users", new { name = "a", email = "a@b.co", password = "StrongPass123!" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUser_WithoutPassword_ReturnsValidationError()
    {
        var admin = await LoginAsync("adel.sebti@corp.internal");

        var res = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "No Pass", email = $"nopass.{Guid.NewGuid():N}@enterprise.internal", role = "End User"
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUser_WithAdministratorRole_IsRejected_SoPrincipalAdminStaysUnique()
    {
        var admin = await LoginAsync("adel.sebti@corp.internal");

        var res = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "Second Admin", email = $"admin2.{Guid.NewGuid():N}@enterprise.internal",
            role = "Administrator", password = "StrongPass123!"
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PrincipalAdmin_CannotBeDeleted()
    {
        var admin = await LoginAsync("adel.sebti@corp.internal");
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/api/users");
        var principal = users!.Single(u => u.Role == "Administrator");

        var res = await admin.DeleteAsync($"/api/users/{principal.Id}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("karim.khmissi@corp.internal")]   // Process Manager
    [InlineData("k.khmissi@enterprise.internal")] // Workflow Designer
    public async Task NonPrincipalUsers_CannotCreateDesignAssets(string email)
    {
        var client = await LoginAsync(email);

        var formRes = await client.PostAsJsonAsync("/api/smartforms", new { name = "BlockedForm" });
        var soRes = await client.PostAsJsonAsync("/api/smartobjects", new { name = "BlockedSo" });
        var connRes = await client.PostAsJsonAsync("/api/connections", new { name = "BlockedConn" });

        formRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        soRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        connRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsers_NonAdmin_ReturnsForbidden_WhileSearchReturnsOk()
    {
        var nonAdmin = await LoginAsync("karim.khmissi@corp.internal");

        (await nonAdmin.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await nonAdmin.GetAsync("/api/users/search?q=karim")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateUser_WithInvalidRole_OrWeakPassword_ReturnsBadRequest()
    {
        var admin = await LoginAsync("adel.sebti@corp.internal");

        var badRole = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "Bad Role",
            email = $"badrole.{Guid.NewGuid():N}@enterprise.internal",
            role = "SuperHero",
            password = "StrongPass123!"
        });
        badRole.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var weakPass = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "Weak Pass",
            email = $"weakpass.{Guid.NewGuid():N}@enterprise.internal",
            role = "End User",
            password = "alllowercase"
        });
        weakPass.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeactivatedUser_ExistingJwtToken_IsRejectedImmediately()
    {
        var admin = await LoginAsync("adel.sebti@corp.internal");
        string email = $"deactivate.{Guid.NewGuid():N}@enterprise.internal";

        var createRes = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "To Deactivate",
            email,
            role = "End User",
            group = "General",
            status = "Active",
            password = "StrongPass123!"
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<UserDto>();

        var userClient = await LoginAsync(email, "StrongPass123!");
        (await userClient.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Admin deactivates the user via PUT /api/users/{id}
        var updateRes = await admin.PutAsJsonAsync($"/api/users/{created!.Id}", new
        {
            name = created.Name,
            role = created.Role,
            group = created.Group,
            status = "Inactive"
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Existing token is immediately rejected
        (await userClient.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_EnforcesComplexity_AndWorksWithNewPassword()
    {
        var admin = await LoginAsync("adel.sebti@corp.internal");
        string email = $"chgpass.{Guid.NewGuid():N}@enterprise.internal";

        var createRes = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "Change Pass User",
            email,
            role = "End User",
            group = "General",
            status = "Active",
            password = "OldPassword1!"
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var userClient = await LoginAsync(email, "OldPassword1!");

        // Weak password rejected
        var weakRes = await userClient.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("OldPassword1!", "weakpass"));
        weakRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Strong password accepted
        var okRes = await userClient.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("OldPassword1!", "NewStrongPass2!"));
        okRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Can sign in with the new password
        var relogin = await LoginAsync(email, "NewStrongPass2!");
        (await relogin.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AccountLockout_AfterFailedAttempts_Returns423_AndAdminResetUnlocks()
    {
        var admin = await LoginAsync("adel.sebti@corp.internal");
        string email = $"lockout.{Guid.NewGuid():N}@enterprise.internal";

        var createRes = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "Lockout Target",
            email,
            role = "End User",
            group = "General",
            status = "Active",
            password = "StrongPass123!"
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<UserDto>();

        var anon = _factory.CreateClient();
        for (int i = 0; i < 5; i++)
        {
            var fail = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPass999!"));
            fail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // 6th attempt returns 423 Locked even with the right password
        var locked = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "StrongPass123!"));
        locked.StatusCode.Should().Be((HttpStatusCode)423);

        // Admin resets password -> clears lockout
        var resetRes = await admin.PostAsJsonAsync($"/api/users/{created!.Id}/reset-password",
            new ResetPasswordRequest("UnlockedPass123!"));
        resetRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var unlocked = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "UnlockedPass123!"));
        unlocked.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteUser_WithLinkedInstance_ReturnsConflict_AndAuditLogRecordsEvents()
    {
        var admin = await LoginAsync("adel.sebti@corp.internal");
        string email = $"linked.{Guid.NewGuid():N}@enterprise.internal";

        var createRes = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "Linked Worker",
            email,
            role = "End User",
            group = "Operations",
            status = "Active",
            password = "StrongPass123!"
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<UserDto>();

        // Create a simple workflow and start an instance as this user
        string wfKey = $"wf-lock-{Guid.NewGuid():N}"[..20];
        const string graphJson = """
        {
          "version": "1.0",
          "nodes": [
            { "id": "start-1", "type": "trigger", "position": { "x": 0, "y": 0 }, "data": { "nodeType": "trigger", "label": "Start" } },
            { "id": "end-1", "type": "end", "position": { "x": 200, "y": 0 }, "data": { "nodeType": "end", "label": "End", "endData": { "status": "completed" } } }
          ],
          "edges": [
            { "id": "e1", "source": "start-1", "target": "end-1" }
          ]
        }
        """;
        var defRes = await admin.PostAsJsonAsync("/api/definitions", new
        {
            workflowKey = wfKey,
            name = "Linked Check WF",
            graphJson,
            createdBy = "adel.sebti@corp.internal",
            category = "Testing"
        });
        defRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var userClient = await LoginAsync(email, "StrongPass123!");
        var startRes = await userClient.PostAsJsonAsync("/api/instances/start", new
        {
            workflowKey = wfKey,
            initiatorId = email
        });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Hard delete must be refused with 409 Conflict
        var delRes = await admin.DeleteAsync($"/api/users/{created!.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Audit log contains the creation event recorded with admin as actor
        var audit = await admin.GetFromJsonAsync<List<UserAuditDto>>("/api/users/audit?limit=50");
        audit.Should().NotBeNull();
        audit!.Should().Contain(a => a.Action == "UserCreated" && a.TargetEmail == email && a.ActorEmail == "adel.sebti@corp.internal");
    }
}
