using FluentAssertions;
using WorkflowEngine.Domain.Entities;
using Xunit;

namespace WorkflowEngine.Domain.UnitTests.Entities;

public class WorkflowDefinitionTests
{
    [Fact]
    public void Constructor_WithValidArguments_InitializesCorrectly()
    {
        // Act
        var definition = new WorkflowDefinition(
            "onboarding-key",
            "Employee Onboarding",
            "admin@corp.internal",
            "Standard HR Onboarding Workflow",
            "HR");

        // Assert
        definition.WorkflowKey.Should().Be("onboarding-key");
        definition.Name.Should().Be("Employee Onboarding");
        definition.CreatedBy.Should().Be("admin@corp.internal");
        definition.Description.Should().Be("Standard HR Onboarding Workflow");
        definition.Category.Should().Be("HR");
        definition.Versions.Should().BeEmpty();
    }

    [Fact]
    public void AddVersion_WhenActive_DeactivatesPreviousVersionsAndIncrementsVersionNumber()
    {
        // Arrange
        var definition = new WorkflowDefinition("wf-1", "Test Workflow", "admin");

        // Act
        var v1 = definition.AddVersion("{\"nodes\":[]}", "admin", "Initial", setActive: true);
        var v2 = definition.AddVersion("{\"nodes\":[{\"id\":\"1\"}]}", "admin", "Update", setActive: true);

        // Assert
        v1.VersionNumber.Should().Be(1);
        v1.IsActive.Should().BeFalse();
        v2.VersionNumber.Should().Be(2);
        v2.IsActive.Should().BeTrue();
        definition.Versions.Should().HaveCount(2);
    }
}
