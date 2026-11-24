using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Infrastructure.IntegrationTests.Persistence;

public class DbContextMappingTests
{
    [Fact]
    public void ModelBuilder_MapsAllEntitiesWithCorrectTableNamesAndKeys()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<WorkflowEngineDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=postgres;Password=postgres")
            .Options;

        // Act
        using var context = new WorkflowEngineDbContext(options);
        var model = context.Model;

        // Assert
        var defEntity = model.FindEntityType(typeof(WorkflowDefinition));
        defEntity.Should().NotBeNull();
        defEntity!.GetTableName().Should().Be("WorkflowDefinitions");
        defEntity.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Contain("DefinitionId");

        var verEntity = model.FindEntityType(typeof(WorkflowVersion));
        verEntity.Should().NotBeNull();
        verEntity!.GetTableName().Should().Be("WorkflowVersions");
        verEntity.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Contain("VersionId");

        var instEntity = model.FindEntityType(typeof(WorkflowInstance));
        instEntity.Should().NotBeNull();
        instEntity!.GetTableName().Should().Be("WorkflowInstances");
        instEntity.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Contain("InstanceId");

        var taskEntity = model.FindEntityType(typeof(WorkflowTask));
        taskEntity.Should().NotBeNull();
        taskEntity!.GetTableName().Should().Be("WorkflowTasks");
        taskEntity.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Contain("TaskId");

        var errEntity = model.FindEntityType(typeof(WorkflowError));
        errEntity.Should().NotBeNull();
        errEntity!.GetTableName().Should().Be("WorkflowInstanceErrors");
        errEntity.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Contain("ErrorId");

        var auditEntity = model.FindEntityType(typeof(WorkflowAuditLog));
        auditEntity.Should().NotBeNull();
        auditEntity!.GetTableName().Should().Be("WorkflowAuditLogs");
        auditEntity.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Contain("LogId");
    }
}
