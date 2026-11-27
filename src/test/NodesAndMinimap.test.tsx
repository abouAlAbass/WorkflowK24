import { describe, it, expect } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { ReactFlowProvider } from '@xyflow/react';
import { StartNode } from '../components/nodes/StartNode';
import { ApprovalNode } from '../components/nodes/ApprovalNode';
import { DecisionNode } from '../components/nodes/DecisionNode';
import { SendEmailNode } from '../components/nodes/SendEmailNode';
import { EndNode } from '../components/nodes/EndNode';

describe('Custom Nodes Readability & Typography', () => {
  it('renders StartNode with >= 12px secondary text and title', () => {
    const data: any = {
      label: 'Employee Form Submission',
      nodeType: 'trigger',
      triggerData: {
        triggerType: 'form',
        eventName: 'On_Employee_Submit',
        variables: [{ name: 'EmployeeName', type: 'string' }]
      }
    };

    render(
      <ReactFlowProvider>
        <StartNode id="test-start" data={data} selected={false} type="trigger" zIndex={1} isConnectable={true} positionAbsoluteX={0} positionAbsoluteY={0} dragging={false} draggable={true} selectable={true} deletable={true} />
      </ReactFlowProvider>
    );

    const title = screen.getByText('Employee Form Submission');
    expect(title).toBeInTheDocument();
    expect(title.className).toContain('text-sm'); // 14px >= 13px

    expect(screen.getByText('Trigger Type:')).toHaveClass('text-xs'); // 12px >= 12px
    expect(screen.getByText('Event Name:')).toHaveClass('text-xs');
  });

  it('renders ApprovalNode with assignee and dynamic outcome handles', () => {
    const data: any = {
      label: 'Manager Approval',
      nodeType: 'approval',
      approvalData: {
        assignmentType: 'user',
        assignee: 'manager@company.com',
        outcomes: [
          { id: 'appr', label: 'Approve Request', color: '#059669' },
          { id: 'rej', label: 'Reject Request', color: '#DC2626' }
        ]
      }
    };

    render(
      <ReactFlowProvider>
        <ApprovalNode id="test-appr" data={data} selected={false} type="approval" zIndex={1} isConnectable={true} positionAbsoluteX={0} positionAbsoluteY={0} dragging={false} draggable={true} selectable={true} deletable={true} />
      </ReactFlowProvider>
    );

    expect(screen.getByText('Manager Approval')).toBeInTheDocument();
    expect(screen.getByText('manager@company.com')).toBeInTheDocument();
    expect(screen.getByText('Approve Request')).toBeInTheDocument();
    expect(screen.getByText('Reject Request')).toBeInTheDocument();
  });

  it('renders DecisionNode with rule conditions and branch handles', () => {
    const data: any = {
      label: 'Budget Threshold',
      nodeType: 'decision',
      decisionData: {
        conditions: [{ field: 'Amount', operator: 'greater_than', value: '5000' }],
        trueOutcomeLabel: 'Over Budget',
        falseOutcomeLabel: 'Within Budget'
      }
    };

    render(
      <ReactFlowProvider>
        <DecisionNode id="test-dec" data={data} selected={false} type="decision" zIndex={1} isConnectable={true} positionAbsoluteX={0} positionAbsoluteY={0} dragging={false} draggable={true} selectable={true} deletable={true} />
      </ReactFlowProvider>
    );

    expect(screen.getByText('Budget Threshold')).toBeInTheDocument();
    expect(screen.getByText('Amount greater than 5000')).toBeInTheDocument();
    expect(screen.getByText('Over Budget')).toBeInTheDocument();
    expect(screen.getByText('Within Budget')).toBeInTheDocument();
  });

  it('renders SendEmailNode with recipient and subject', () => {
    const data: any = {
      label: 'Send Notification',
      nodeType: 'sendEmail',
      sendEmailData: {
        to: 'hr@company.com',
        subject: 'Welcome Package'
      }
    };

    render(
      <ReactFlowProvider>
        <SendEmailNode id="test-email" data={data} selected={false} type="sendEmail" zIndex={1} isConnectable={true} positionAbsoluteX={0} positionAbsoluteY={0} dragging={false} draggable={true} selectable={true} deletable={true} />
      </ReactFlowProvider>
    );

    expect(screen.getByText('Send Notification')).toBeInTheDocument();
    expect(screen.getByText('hr@company.com')).toBeInTheDocument();
    expect(screen.getByText('Welcome Package')).toBeInTheDocument();
  });

  it('renders EndNode with terminal status badge', () => {
    const data: any = {
      label: 'Process Completed',
      nodeType: 'end',
      endData: {
        status: 'completed',
        message: 'All tasks finalized.'
      }
    };

    render(
      <ReactFlowProvider>
        <EndNode id="test-end" data={data} selected={false} type="end" zIndex={1} isConnectable={true} positionAbsoluteX={0} positionAbsoluteY={0} dragging={false} draggable={true} selectable={true} deletable={true} />
      </ReactFlowProvider>
    );

    expect(screen.getByText('Process Completed')).toBeInTheDocument();
    expect(screen.getByText('completed')).toBeInTheDocument();
    expect(screen.getByText('"All tasks finalized."')).toBeInTheDocument();
  });
});
