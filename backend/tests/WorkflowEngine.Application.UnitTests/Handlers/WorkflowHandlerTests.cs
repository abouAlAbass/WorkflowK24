using FluentAssertions;
using Moq;
using WorkflowEngine.Application.Features.Definitions.Commands;
using WorkflowEngine.Application.Features.Definitions.Handlers;
using WorkflowEngine.Application.Features.Instances.Commands;
using WorkflowEngine.Application.Features.Instances.Handlers;
using WorkflowEngine.Application.Features.SmartForms.Validation;
using WorkflowEngine.Application.Features.Tasks.Commands;
using WorkflowEngine.Application.Features.Tasks.Handlers;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;
using Xunit;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Application.UnitTests.Handlers;

public class WorkflowHandlerTests
{
    [Fact]
    public async Task CreateWorkflowDefinitionHandler_CreatesDefinitionAndVersion()
    {
        // Arrange
        var defRepoMock = new Mock<IWorkflowDefinitionRepository>();
        var uowMock = new Mock<IUnitOfWork>();

        defRepoMock.Setup(r => r.GetByKeyWithVersionsAsync("new-wf", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowDefinition?)null);

        var handler = new WorkflowDefinitionHandlers(defRepoMock.Object, uowMock.Object);
        var command = new CreateWorkflowDefinitionCommand("new-wf", "New Workflow", "{\"nodes\":[]}", "creator");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.WorkflowKey.Should().Be("new-wf");
        result.VersionNumber.Should().Be(1);
        defRepoMock.Verify(r => r.Add(It.IsAny<WorkflowDefinition>()), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartWorkflowInstanceHandler_WhenActiveVersionExists_StartsInstance()
    {
        // Arrange
        var defRepoMock = new Mock<IWorkflowDefinitionRepository>();
        var instRepoMock = new Mock<IWorkflowInstanceRepository>();
        var execEngineMock = new Mock<IWorkflowExecutionEngine>();
        var uowMock = new Mock<IUnitOfWork>();

        var def = new WorkflowDefinition("order-wf", "Order Process", "admin");
        var version = def.AddVersion("{\"nodes\":[{\"id\":\"start-1\",\"type\":\"trigger\",\"position\":{\"x\":0,\"y\":0},\"data\":{\"nodeType\":\"trigger\",\"label\":\"Start\"}}],\"edges\":[]}", "admin");

        defRepoMock.Setup(r => r.GetActiveVersionByKeyAsync("order-wf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        var handler = new WorkflowInstanceHandlers(defRepoMock.Object, instRepoMock.Object, execEngineMock.Object, uowMock.Object);
        var command = new StartWorkflowInstanceCommand("order-wf", "initiator@corp.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Running");
        instRepoMock.Verify(r => r.Add(It.IsAny<WorkflowInstance>()), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteTaskHandler_WhenPendingTask_CompletesAndAdvances()
    {
        // Arrange
        var taskRepoMock = new Mock<IWorkflowTaskRepository>();
        var instRepoMock = new Mock<IWorkflowInstanceRepository>();
        var execEngineMock = new Mock<IWorkflowExecutionEngine>();
        var uowMock = new Mock<IUnitOfWork>();

        var instance = new WorkflowInstance("inst-1", 1, "wf-1", "user", null);
        var task = new WorkflowTask("task-123", "inst-1", "node-appr", "Approval", AssigneeType.User, "approver@corp.com", null);

        taskRepoMock.Setup(r => r.GetByIdWithInstanceAsync("task-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var handler = new WorkflowTaskHandlers(
            taskRepoMock.Object,
            instRepoMock.Object,
            execEngineMock.Object,
            uowMock.Object,
            new Mock<ISmartFormRepository>().Object,
            new SmartFormValidationEngine());
        var command = new CompleteTaskCommand(
            TaskId: "task-123",
            Outcome: "Approve",
            CurrentUserId: "approver@corp.com",
            CurrentUserRoles: new[] { "Process Manager" },
            Comments: "Approved!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.TaskId.Should().Be("task-123");
        result.Outcome.Should().Be("Approve");
        task.Status.Should().Be(TaskStatus.Approved);
        task.CompletedBy.Should().Be("approver@corp.com");
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
