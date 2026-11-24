using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Infrastructure;
using Xunit;

namespace WorkflowEngine.Infrastructure.IntegrationTests.Persistence;

public class MultiProviderConfigurationTests
{
    [Fact]
    public void AddInfrastructure_WithSqlServerProvider_ConfiguresSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SqlServer",
                ["Database:ConnectionString"] = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;"
            })
            .Build();

        // Act
        services.AddInfrastructure(config);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_WithPostgreSqlProvider_ConfiguresSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "PostgreSql",
                ["Database:ConnectionString"] = "Host=localhost;Database=testdb;Username=postgres;Password=postgres"
            })
            .Build();

        // Act
        services.AddInfrastructure(config);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_WithInvalidProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Oracle",
                ["Database:ConnectionString"] = "Data Source=test;"
            })
            .Build();

        // Act
        Action act = () => services.AddInfrastructure(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported database provider 'Oracle'*");
    }
}
