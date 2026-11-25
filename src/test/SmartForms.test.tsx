import { describe, it, expect } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { SmartFormsView } from '../components/views/SmartFormsView';
import { TaskCenterView } from '../components/views/TaskCenterView';
import { SmartFormRenderer, evaluateFieldVisibility } from '../components/forms/SmartFormRenderer';
import { useWorkflowStore } from '../hooks/useWorkflowStore';
import type { SmartFormDefinition } from '../types/smartform';

describe('No-Code SmartForms Designer & Conditional Runtime Renderer', () => {
  it('renders SmartForms studio with seeded forms, toolbox controls, and live preview switch', () => {
    render(<SmartFormsView />);

    expect(screen.getByTestId('generate-from-so-btn')).toBeInTheDocument();
    expect(screen.getByTestId('palette-add-text')).toBeInTheDocument();
    expect(screen.getByTestId('palette-add-repeater')).toBeInTheDocument();
    expect(screen.getByTestId('palette-add-file')).toBeInTheDocument();

    // Add a new text control via toolbox click
    fireEvent.click(screen.getByTestId('palette-add-text'));
    expect(screen.getAllByText(/New Text Field/i).length).toBeGreaterThan(0);

    // Switch to Live Preview & Run mode
    fireEvent.click(screen.getByTestId('form-mode-preview'));
    expect(screen.getByText(/Add Line Item/i)).toBeInTheDocument();
  });

  it('evaluates conditional visibility rules dynamically as form values change', () => {
    expect(
      evaluateFieldVisibility(
        { field: 'Amount', operator: 'greater_than', value: '5000' },
        { Amount: '4500' }
      )
    ).toBe(false);

    expect(
      evaluateFieldVisibility(
        { field: 'Amount', operator: 'greater_than', value: '5000' },
        { Amount: '7500' }
      )
    ).toBe(true);

    const conditionalForm: SmartFormDefinition = {
      id: 'form-cond-test',
      name: 'CondTest',
      title: 'Conditional Test Form',
      description: 'Test conditional rendering',
      category: 'Finance',
      status: 'Published',
      submissionsCount: 0,
      updatedAt: new Date().toISOString(),
      fields: [
        {
          id: 'f-amt',
          name: 'Amount',
          label: 'Request Amount',
          type: 'number',
          required: true,
          readOnly: false,
          width: 'half',
          defaultValue: '1000'
        },
        {
          id: 'f-just',
          name: 'CfoJustification',
          label: 'CFO Executive Note',
          type: 'textarea',
          required: false,
          readOnly: false,
          width: 'full',
          visibilityRule: { field: 'Amount', operator: 'greater_than', value: '5000' }
        }
      ]
    };

    render(<SmartFormRenderer form={conditionalForm} />);

    // Initially Amount = 1000 -> CFO Executive Note is hidden
    expect(screen.queryByText('CFO Executive Note')).not.toBeInTheDocument();

    // Update Amount to 9000 -> CFO Executive Note dynamically appears
    const numInput = screen.getByDisplayValue('1000');
    fireEvent.change(numInput, { target: { value: '9000' } });
    expect(screen.getByText('CFO Executive Note')).toBeInTheDocument();
  });

  it('binds TaskCenterView directly to task.formId, task.formMode, and task.formData and captures editable formPatch', () => {
    let capturedArgs: {
      taskId: string;
      outcomeId: string;
      comment?: string;
      formPatch?: Record<string, unknown>;
    } | null = null;

    const originalResolve = useWorkflowStore.getState().resolveTask;
    const originalTasks = useWorkflowStore.getState().actionItemTasks;
    act(() => {
      useWorkflowStore.setState({
        actionItemTasks: [
          {
            id: 'TASK-EDIT-1',
            instanceId: 'INST-EDIT-1',
            title: 'Review Vendor PO',
            workflowName: 'Custom Procurement Workflow',
            requester: 'adel.sebti@corp.internal',
            requesterAvatar: '/avatars/user-2.svg',
            department: 'Finance',
            requestedAt: '2026-11-24T10:00:00Z',
            dueDate: 'Tomorrow',
            status: 'PENDING',
            priority: 'High',
            description: 'Verify negotiated quote',
            outcomes: [{ id: 'approve', label: 'Approve PO', color: '#059669' }],
            formId: 'form-purchase-req',
            formMode: 'editable',
            formData: {
              VendorName: 'Lenovo Enterprise',
              Department: 'Finance',
              Amount: '6200'
            }
          }
        ],
        resolveTask: (taskId, outcomeId, comment, formPatch) => {
          capturedArgs = { taskId, outcomeId, comment, formPatch };
        }
      });
    });

    try {
      render(<TaskCenterView />);

      // Renders real instance data from task.formData (not hardcoded dummy values)
      const vendorInput = screen.getByDisplayValue('Lenovo Enterprise') as HTMLInputElement;
      expect(vendorInput).not.toBeDisabled();

      // Edit vendor name in editable task mode
      fireEvent.change(vendorInput, { target: { value: 'Lenovo Global Corp' } });

      fireEvent.click(screen.getByText('Approve PO'));
      fireEvent.click(screen.getByText('Confirm Approve PO'));

      expect(capturedArgs).not.toBeNull();
      expect(capturedArgs!.taskId).toBe('TASK-EDIT-1');
      expect(capturedArgs!.outcomeId).toBe('approve');
      expect(capturedArgs!.formPatch).toEqual({ VendorName: 'Lenovo Global Corp' });
    } finally {
      act(() => {
        useWorkflowStore.setState({ resolveTask: originalResolve, actionItemTasks: originalTasks });
      });
    }
  });

  it('strips hidden phantom values on submit and displays structured 400 validation errors', async () => {
    const testForm: SmartFormDefinition = {
      id: 'form-phantom-test',
      name: 'PhantomTest',
      title: 'Phantom Sanitization Form',
      description: 'Verifies hidden values are stripped before onSubmit',
      category: 'Finance',
      status: 'Published',
      submissionsCount: 0,
      updatedAt: new Date().toISOString(),
      fields: [
        {
          id: 'f-amt',
          name: 'Amount',
          label: 'Order Amount',
          type: 'number',
          required: true,
          readOnly: false,
          width: 'half',
          defaultValue: '9000'
        },
        {
          id: 'f-just',
          name: 'HiddenWhenLow',
          label: 'Executive Note',
          type: 'textarea',
          required: false,
          readOnly: false,
          width: 'full',
          visibilityRule: { field: 'Amount', operator: 'greater_than', value: '5000' }
        }
      ]
    };

    let submittedPayload: Record<string, unknown> | null = null;
    render(
      <SmartFormRenderer
        form={testForm}
        onSubmit={async (vals) => {
          submittedPayload = vals;
          const err = new Error('Server rejected Amount') as Error & { fieldErrors?: Record<string, string> };
          err.fieldErrors = { Amount: 'Amount exceeds department limit.' };
          throw err;
        }}
      />
    );

    // Initially Amount = 9000 -> Executive Note is visible; user types a secret note
    const noteInput = screen.getByRole('textbox');
    fireEvent.change(noteInput, { target: { value: 'Secret phantom text' } });

    // User lowers Amount to 2000 -> Executive Note becomes hidden
    const amtInput = screen.getByDisplayValue('9000');
    fireEvent.change(amtInput, { target: { value: '2000' } });

    // Submit form
    await act(async () => {
      fireEvent.click(screen.getByText(/Submit Form & Start Process/i));
    });

    expect(submittedPayload).toEqual({ Amount: '2000' });
    expect(submittedPayload).not.toHaveProperty('HiddenWhenLow');
    expect(screen.getByText('Amount exceeds department limit.')).toBeInTheDocument();
  });

  it('displays Draft/Published version badge and triggers publishing a new version', async () => {
    render(<SmartFormsView />);

    // Initially seeded form is v1 • Published
    const badge = screen.getByTestId('smartform-version-badge');
    expect(badge).toHaveTextContent(/v\d+\s*•\s*(Published|Draft)/i);

    // Adding a field transitions the form into an unpublished Draft version
    fireEvent.click(screen.getByTestId('palette-add-text'));
    expect(screen.getByTestId('smartform-version-badge')).toHaveTextContent(/Draft/i);

    const publishBtn = screen.getByTestId('publish-smartform-btn');
    expect(publishBtn).not.toBeDisabled();

    // Clicking Publish Version activates the new version as Published
    await act(async () => {
      fireEvent.click(publishBtn);
    });

    expect(screen.getByTestId('smartform-version-badge')).toHaveTextContent(/Published/i);
    expect(screen.getByTestId('smartform-version-selector')).toBeInTheDocument();
  });
});
