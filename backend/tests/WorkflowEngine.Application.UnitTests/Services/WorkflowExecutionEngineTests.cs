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

        var nextNode = _engine.EvaluateNextNode(graph, "dec-1", null, "{\"amount\": 5000}");

        nextNode.Should().Be("node-high");
    }

    [Fact]
    public void EvaluateNextNode_ForDecisionNode_WhenConditionFalse_SelectsFalseHandle()
    {
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

        var nextNode = _engine.EvaluateNextNode(graph, "dec-1", null, "{\"amount\": 500}");

        nextNode.Should().Be("node-low");
    }

    [Fact]
    public void EvaluateConditionRule_MultiConditionsWithAndOr_EvaluatesAllConditionsAccurately()
    {
        var andDecision = new DecisionDataPayload(
            new List<DecisionConditionDto>
            {
                new("Amount", "greater_than_or_equal", "5000"),
                new("user.department", "equals", "Finance"),
                new("notes", "is_not_empty", "")
            },
            "Yes",
            "No",
            "AND");

        // Fails because department is IT, even though Amount >= 5000
        WorkflowExecutionEngine.EvaluateConditionRule(
            andDecision,
            "{\"amount\": 5000, \"user\": {\"department\": \"IT\"}, \"notes\": \"Urgent\"}")
            .Should().BeFalse();

        // Succeeds when all 3 conditions match (including case-insensitive 'Amount' vs 'amount')
        WorkflowExecutionEngine.EvaluateConditionRule(
            andDecision,
            "{\"amount\": 5000, \"user\": {\"department\": \"finance\"}, \"notes\": \"Urgent\"}")
            .Should().BeTrue();

        var orDecision = andDecision with { LogicalOperator = "OR" };
        WorkflowExecutionEngine.EvaluateConditionRule(
            orDecision,
            "{\"amount\": 100, \"user\": {\"department\": \"Finance\"}, \"notes\": \"\"}")
            .Should().BeTrue();
    }

    [Fact]
    public void EvaluateNextNode_ForApprovalNode_MatchesSelectedOutcomeHandle()
    {
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

        var nextNode = _engine.EvaluateNextNode(graph, "appr-1", "Approve");

        nextNode.Should().Be("node-approved");
    }

    [Fact]
    public async Task ExecuteNodeLogicAsync_WhenApprovalNode_CreatesTaskAndPauses()
    {
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

        await _engine.ExecuteNodeLogicAsync(instance, graph, "appr-1");

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

        await _engine.ExecuteNodeLogicAsync(instance, graph, "stage-1");

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.CurrentNodeId.Should().Be("final-end");
        instance.GetStageStack().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAndResume_WhenStageContainsApproval_PausesParentUntilSubWorkflowCompletes()
    {
        // Arrange: Stage-1 (sub-trig -> sub-appr -> sub-email -> sub-end) -> parent-appr -> final-end
        var subNodes = new List<ReactFlowNode>
        {
            new("sub-trig", "trigger", new(0, 0), new("trigger", "Sub Start")),
            new("sub-appr", "approval", new(100, 0), new("approval", "Security Officer Sign-off",
                ApprovalData: new ApprovalDataPayload("user", "sec@corp.com", new List<ApprovalOutcomeDto>
                {
                    new("approve", "Approve", "#059669")
                }))),
            new("sub-email", "sendEmail", new(200, 0), new("sendEmail", "Notify Cleared",
                SendEmailData: new SendEmailDataPayload("ops@corp.com", "Cleared", "Security approved"))),
            new("sub-end", "end", new(300, 0), new("end", "Sub End"))
        };
        var subEdges = new List<ReactFlowEdge>
        {
            new("se-1", "sub-trig", "sub-appr"),
            new("se-2", "sub-appr", "sub-email", "approve", null, "Approve"),
            new("se-3", "sub-email", "sub-end")
        };

        var rootGraph = new ReactFlowGraph(
            "wf-parent", "Parent Process", "1.0",
            new List<ReactFlowNode>
            {
                new("stage-1", "stage", new(0, 0), new("stage", "Security Stage",
                    StageData: new StageDataPayload("stage-1", "Security Stage", null, null,
                        new StageSubWorkflowPayload(subNodes, subEdges)))),
                new("parent-appr", "approval", new(200, 0), new("approval", "VP Final Approval",
                    ApprovalData: new ApprovalDataPayload("user", "vp@corp.com", new List<ApprovalOutcomeDto>
                    {
                        new("approve", "Approve", "#059669")
                    }))),
                new("final-end", "end", new(400, 0), new("end", "Workflow Complete"))
            },
            new List<ReactFlowEdge>
            {
                new("pe-1", "stage-1", "parent-appr"),
                new("pe-2", "parent-appr", "final-end", "approve", null, "Approve")
            }
        );

        var instance = new WorkflowInstance("inst-nested", 1, "wf-parent", "init@corp.com", null);

        // Act 1: Start execution at stage-1 -> MUST pause at sub-appr and NOT reach parent-appr or final-end
        await _engine.ExecuteNodeLogicAsync(instance, rootGraph, "stage-1");

        instance.Status.Should().Be(WorkflowStatus.Running);
        instance.CurrentNodeId.Should().Be("sub-appr");
        instance.GetStageStack().Should().Equal("stage-1");
        instance.Tasks.Should().ContainSingle(t => t.NodeId == "sub-appr" && t.Status == TaskStatus.Pending);
        _emailServiceMock.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        // Act 2: Complete sub-appr -> finishes sub-email & sub-end, pops stage-1, and pauses at parent-appr!
        instance.Tasks.First(t => t.NodeId == "sub-appr").Complete("Approve", "sec@corp.com", "LGTM");
        await _engine.ResumeAfterTaskAsync(instance, rootGraph, "sub-appr", "Approve");

        instance.Status.Should().Be(WorkflowStatus.Running);
        instance.CurrentNodeId.Should().Be("parent-appr");
        instance.GetStageStack().Should().BeEmpty();
        _emailServiceMock.Verify(e => e.SendEmailAsync("ops@corp.com", "Cleared", "Security approved", false, It.IsAny<CancellationToken>()), Times.Once);

        // Act 3: Complete parent-appr -> reaches final-end and marks WorkflowInstance Completed!
        instance.Tasks.First(t => t.NodeId == "parent-appr").Complete("Approve", "vp@corp.com", "Approved");
        await _engine.ResumeAfterTaskAsync(instance, rootGraph, "parent-appr", "Approve");

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.CurrentNodeId.Should().Be("final-end");
    }

    [Fact]
    public async Task ExecuteNodeLogicAsync_WhenCyclicGraphDetected_RecordsMaxStepsFailureWithoutStackOverflow()
    {
        var cyclicGraph = new ReactFlowGraph(
            "wf-cycle", "Infinite Loop", "1.0",
            new List<ReactFlowNode>
            {
                new("dec-a", "decision", new(0, 0), new("decision", "Loop A", DecisionData: new DecisionDataPayload(new(), "Yes", "No"))),
                new("dec-b", "decision", new(100, 0), new("decision", "Loop B", DecisionData: new DecisionDataPayload(new(), "Yes", "No")))
            },
            new List<ReactFlowEdge>
            {
                new("e1", "dec-a", "dec-b", "decision-true"),
                new("e2", "dec-b", "dec-a", "decision-true")
            }
        );

        var instance = new WorkflowInstance("inst-loop", 1, "wf-cycle", "init@corp.com", null);

        await _engine.ExecuteNodeLogicAsync(instance, cyclicGraph, "dec-a");

        instance.Status.Should().Be(WorkflowStatus.Failed);
        instance.Errors.Should().ContainSingle(e => e.ErrorCode == "MAX_STEPS_EXCEEDED");
    }

    [Fact]
    public async Task ExecuteNodeLogicAsync_ForSendEmailNode_InterpolatesDynamicVariables()
    {
        var graph = new ReactFlowGraph(
            "wf-mail", "Email Template Test", "1.0",
            new List<ReactFlowNode>
            {
                new("email-1", "sendEmail", new(0, 0), new("sendEmail", "Send PO Email",
                    SendEmailData: new SendEmailDataPayload(
                        "{{approver.email}}",
                        "PO #{{poNumber}} from {{requester}} (${{amount}})",
                        "Instance {{instanceId}} requires review for {{approver.department}}.",
                        true))),
                new("end-1", "end", new(100, 0), new("end", "Done"))
            },
            new List<ReactFlowEdge>
            {
                new("e1", "email-1", "end-1")
            }
        );

        var instance = new WorkflowInstance(
            "inst-mail-42",
            1,
            "wf-mail",
            "karim@corp.com",
            "{\"poNumber\": \"PO-990\", \"amount\": 12500, \"approver\": {\"email\": \"cfo@corp.com\", \"department\": \"Finance\"}}");

        await _engine.ExecuteNodeLogicAsync(instance, graph, "email-1");

        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "cfo@corp.com",
            "PO #PO-990 from karim@corp.com ($12500)",
            "Instance inst-mail-42 requires review for Finance.",
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        instance.Status.Should().Be(WorkflowStatus.Completed);
    }
}
