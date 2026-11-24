using FluentAssertions;
using Moq;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.ValueObjects;
using Xunit;
using TaskStatus = WorkflowEngine.Domain.Enums.TaskStatus;

namespace WorkflowEngine.Application.UnitTests.Services;

public class WorkflowExecutionEngineTests
{
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly WorkflowExecutionEngine _engine;

    public WorkflowExecutionEngineTests()
    {
        _emailServiceMock = new Mock<IEmailService>();
        _engine = new WorkflowExecutionEngine(_emailServiceMock.Object);
    }

    [Fact]
    public void EvaluateNextNode_ForDecisionNode_WhenConditionTrue_SelectsTrueHandle()
    {
        // Arrange
        var graph = new ReactFlowGraph(
            "wf-1", "Test", "1.0",
            new List<ReactFlowNode>
            {
                new("dec-1", "decision", new(0, 0), new("decision", "Amount Check", null, null, null, null,
                    new DecisionDataPayload(
                        new List<DecisionConditionDto> { new("amount", "greater_than", "1000") },
                        "Approve",
                        "Reject")))
            },
            new List<ReactFlowEdge>
            {
                new("e1", "dec-1", "node-high", "decision-true"),
                new("e2", "dec-1", "node-low", "decision-false")
            }
        );

        // Act
        var nextNode = _engine.EvaluateNextNode(graph, "dec-1", null, "{\"amount\": 5000}");

        // Assert
        nextNode.Should().Be("node-high");
    }

    [Fact]
    public void EvaluateNextNode_ForDecisionNode_WhenConditionFalse_SelectsFalseHandle()
    {
        // Arrange
        var graph = new ReactFlowGraph(
            "wf-1", "Test", "1.0",
            new List<ReactFlowNode>
            {
                new("dec-1", "decision", new(0, 0), new("decision", "Amount Check", null, null, null, null,
                    new DecisionDataPayload(
                        new List<DecisionConditionDto> { new("amount", "greater_than", "1000") },
                        "Approve",
                        "Reject")))
            },
            new List<ReactFlowEdge>
            {
                new("e1", "dec-1", "node-high", "decision-true"),
                new("e2", "dec-1", "node-low", "decision-false")
            }
        );

        // Act
        var nextNode = _engine.EvaluateNextNode(graph, "dec-1", null, "{\"amount\": 500}");

        // Assert
        nextNode.Should().Be("node-low");
    }

    [Fact]
    public void EvaluateNextNode_ForApprovalNode_MatchesSelectedOutcomeHandle()
    {
        // Arrange
        var graph = new ReactFlowGraph(
            "wf-2", "Test", "1.0",
            new List<ReactFlowNode>
            {
                new("appr-1", "approval", new(0, 0), new("approval", "Manager Approval"))
            },
            new List<ReactFlowEdge>
            {
                new("e1", "appr-1", "node-approved", "outcome-approve", null, "Approve"),
                new("e2", "appr-1", "node-rejected", "outcome-reject", null, "Reject")
            }
        );

        // Act
        var nextNode = _engine.EvaluateNextNode(graph, "appr-1", "Approve");

        // Assert
        nextNode.Should().Be("node-approved");
    }

    [Fact]
    public async Task ExecuteNodeLogicAsync_WhenApprovalNode_CreatesTaskAndPauses()
    {
        // Arrange
        var graph = new ReactFlowGraph(
            "wf-3", "Test", "1.0",
            new List<ReactFlowNode>
            {
                new("appr-1", "approval", new(0, 0), new("approval", "Department Approval", null,
                    new ApprovalDataPayload("user", "boss@corp.com", new List<ApprovalOutcomeDto>())))
            },
            new List<ReactFlowEdge>()
        );

        var instance = new WorkflowInstance("inst-1", 1, "test-wf", "requester@corp.com", null);

        // Act
        await _engine.ExecuteNodeLogicAsync(instance, graph, "appr-1");

        // Assert
        instance.CurrentNodeId.Should().Be("appr-1");
        instance.Tasks.Should().ContainSingle();
        var task = instance.Tasks.First();
        task.AssigneeId.Should().Be("boss@corp.com");
        task.TaskTitle.Should().Be("Department Approval");
        task.Status.Should().Be(TaskStatus.Pending);
    }

    [Fact]
    public async Task ExecuteNodeLogicAsync_ForStageNode_TraversesSubWorkflowAndAdvances()
    {
        // Arrange
        var subWorkflowNodes = new List<ReactFlowNode>
        {
            new("sub-trig-1", "trigger", new(0, 0), new("trigger", "Sub Trigger")),
            new("sub-end-1", "end", new(100, 0), new("end", "Sub End"))
        };
        var subWorkflowEdges = new List<ReactFlowEdge>
        {
            new("sub-e1", "sub-trig-1", "sub-end-1")
        };

        var graph = new ReactFlowGraph(
            "wf-1", "Parent Workflow", "1.0",
            new List<ReactFlowNode>
            {
                new("stage-1", "stage", new(0, 0), new("stage", "Security Review Stage",
                    StageData: new StageDataPayload(
                        "stage-1",
                        "Security Review",
                        "Pre-flight security audits",
                        "#6366F1",
                        new StageSubWorkflowPayload(subWorkflowNodes, subWorkflowEdges)
                    ))),
                new("final-end", "end", new(200, 0), new("end", "Final Complete"))
            },
            new List<ReactFlowEdge>
            {
                new("e-stage-to-end", "stage-1", "final-end")
            }
        );

        var instance = new WorkflowInstance("inst-stage-1", 1, "test-wf", "requester@corp.com", null);

        // Act
        await _engine.ExecuteNodeLogicAsync(instance, graph, "stage-1");

        // Assert
        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.CurrentNodeId.Should().Be("final-end");
    }
}

