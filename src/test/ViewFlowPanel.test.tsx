import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { InstanceViewFlowPanel } from '../components/views/InstanceViewFlowPanel';
import { repositoryManager } from '../services/repositoryManager';
import type { ViewFlowData } from '../types/workflow';

const mockFlowData: ViewFlowData = {
  instance: {
    id: 'INST-VF-100',
    status: 'Running',
    suspendedFromStatus: null,
    startedAt: '2026-11-25T10:00:00Z',
    endedAt: null,
    version: 2,
    definitionId: 12,
    definitionVersion: 3
  },
  definitionJson: JSON.stringify({ id: 'wf-1', name: 'Approval Flow', version: '3', nodes: [], edges: [] }),
  nodes: [
    {
      nodeId: 'trig-start',
      type: 'trigger',
      status: 'Completed',
      attempts: 1,
      startedAt: '2026-11-25T10:00:00Z',
      endedAt: '2026-11-25T10:00:01Z'
    },
    {
      nodeId: 'appr-mgr',
      type: 'approval',
      status: 'Waiting',
      attempts: 1,
      startedAt: '2026-11-25T10:00:01Z',
      endedAt: null
    }
  ],
  activeNodes: ['appr-mgr'],
  activeTokens: ['appr-mgr'],
  timeline: [
    {
      id: 101,
      instanceId: 'INST-VF-100',
      seq: 1,
      type: 'Instance.Suspended',
      nodeId: 'appr-mgr',
      actorId: 'adel.sebti@corp.internal',
      actorType: 'user',
      payloadJson: '{"reason":"Audit check"}',
      createdAt: '2026-11-25T10:05:00Z'
    }
  ],
  variables: {
    department: 'Finance',
    salary: '***'
  }
};

describe('InstanceViewFlowPanel', () => {
  const getInstanceFlowMock = vi.fn();
  const cancelInstanceMock = vi.fn();
  const suspendInstanceMock = vi.fn();
  const resumeInstanceMock = vi.fn();
  const retryInstanceMock = vi.fn();

  beforeEach(() => {
    vi.restoreAllMocks();
    getInstanceFlowMock.mockReset().mockResolvedValue(mockFlowData);
    cancelInstanceMock.mockReset().mockResolvedValue({
      instanceId: 'INST-VF-100',
      status: 'Cancelled',
      command: 'Cancel',
      commandId: 'cmd-1',
      executedAt: '2026-11-25T10:10:00Z',
      message: 'Cancelled'
    });
    suspendInstanceMock.mockReset();
    resumeInstanceMock.mockReset();
    retryInstanceMock.mockReset();

    vi.spyOn(repositoryManager, 'getRepository').mockReturnValue({
      getInstanceFlow: getInstanceFlowMock,
      cancelInstance: cancelInstanceMock,
      suspendInstance: suspendInstanceMock,
      resumeInstance: resumeInstanceMock,
      retryInstance: retryInstanceMock
    } as any);
  });

  it('renders ViewFlow status, executed nodes, event timeline, masked variables, and enforces 10-char minimum cancel reason', async () => {
    const onClose = vi.fn();
    render(<InstanceViewFlowPanel instanceId="INST-VF-100" onClose={onClose} />);

    // Wait for ViewFlow projection to render
    await waitFor(() => {
      expect(screen.getByTestId('viewflow-status-badge')).toHaveTextContent('Running');
    });

    // Nodes & Timeline & Variables
    expect(screen.getByText('trig-start')).toBeInTheDocument();
    expect(screen.getByText('appr-mgr')).toBeInTheDocument();
    expect(screen.getByText('Instance.Suspended')).toBeInTheDocument();
    expect(screen.getByText('#1')).toBeInTheDocument();
    expect(screen.getByText('***')).toBeInTheDocument();

    // Contextual buttons for Running status: Suspend & Cancel visible, Resume & Retry hidden
    expect(screen.getByRole('button', { name: /Suspend/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Cancel/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Resume/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Retry/i })).not.toBeInTheDocument();

    // Open Cancel modal/form
    fireEvent.click(screen.getByRole('button', { name: /Cancel/i }));

    const reasonInput = screen.getByLabelText(/Cancellation Reason/i);
    const confirmBtn = screen.getByRole('button', { name: /Confirm Cancel/i });

    // Less than 10 chars -> button disabled and validation alert shown
    fireEvent.change(reasonInput, { target: { value: 'too short' } });
    expect(confirmBtn).toBeDisabled();
    expect(reasonInput).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent(/at least 10 characters/i);
    expect(cancelInstanceMock).not.toHaveBeenCalled();

    // 10+ chars -> valid and submits
    fireEvent.change(reasonInput, { target: { value: 'Cancelled due to duplicate purchase request' } });
    expect(confirmBtn).not.toBeDisabled();
    expect(reasonInput).toHaveAttribute('aria-invalid', 'false');

    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(cancelInstanceMock).toHaveBeenCalledWith(
        'INST-VF-100',
        'Cancelled due to duplicate purchase request'
      );
    });
  });

  it('displays 403/409 operational errors cleanly in role=alert banner', async () => {
    const err403 = Object.assign(new Error('Insufficient permissions'), { status: 403, code: 'FORBIDDEN' });
    suspendInstanceMock.mockRejectedValueOnce(err403);

    render(<InstanceViewFlowPanel instanceId="INST-VF-100" onClose={vi.fn()} />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Suspend/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /Suspend/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/403 Forbidden/i);
    });
  });
});
