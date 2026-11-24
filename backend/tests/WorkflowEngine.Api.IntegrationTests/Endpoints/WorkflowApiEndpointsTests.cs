using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Api.Endpoints;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
    }
}

public class WorkflowApiEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public WorkflowApiEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWithEngineInfo()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
        content.Should().Contain("WorkflowEngine .NET 10");
    }

    [Fact]
    public async Task PostLogin_WithValidEmail_ReturnsJwtToken()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new LoginRequest("adel.sebti@corp.internal", "Password123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.Email.Should().Be("adel.sebti@corp.internal");
        body.Name.Should().Be("Adel Sebti");
    }

    [Fact]
    public async Task PostLogin_WithEmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new LoginRequest("", "");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetSmartObjects_ReturnsOkWithList()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/smartobjects");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostSmartObject_WithValidPayload_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
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

        // Act
        var response = await client.PostAsJsonAsync("/api/smartobjects", newEntity);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetUsers_ReturnsOkWithList()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("a.sebti@enterprise.internal");
    }

    [Fact]
    public async Task PostUser_WithValidPayload_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var newUser = new
        {
            name = "Test Engineer",
            email = $"test.eng.{Guid.NewGuid():N}@enterprise.internal",
            role = "Process Manager",
            group = "Operations",
            status = "Active"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users", newUser);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetAnalyticsKpis_ReturnsOkWithAggregates()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/analytics/kpis");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("avgCompletionTime");
        content.Should().Contain("slaComplianceRate");
    }

    [Fact]
    public async Task GetSettings_ReturnsOkWithConfig()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("smtpHost");
    }

    [Fact]
    public async Task PutSettings_WithValidPayload_ReturnsOkWithUpdatedConfig()
    {
        // Arrange
        var client = _factory.CreateClient();
        var update = new
        {
            smtpHost = "smtp.updated.domain.internal",
            smtpPort = "2525",
            sqlMaxPoolSize = 150
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/settings", update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("smtp.updated.domain.internal");
    }

    [Fact]
    public async Task DeleteUser_ExistingUser_ReturnsNoContent()
    {
        // Arrange
        var client = _factory.CreateClient();
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

        // Act
        var deleteRes = await client.DeleteAsync($"/api/users/{created!.Id}");

        // Assert
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
