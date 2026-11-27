import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { FormControlRegistry } from '../components/forms/FormControlRegistry';
import { SmartFormRenderer } from '../components/forms/SmartFormRenderer';
import { ServicePortalView } from '../components/views/ServicePortalView';
import { useSmartFormStore } from '../store/useSmartFormStore';
import { repositoryManager } from '../services/repositoryManager';
import type { SmartFormDefinition } from '../types/smartform';

describe('FormControlRegistry & Advanced Controls (Step C.3)', () => {
  beforeEach(() => {
    repositoryManager.setMode('demo');
    localStorage.clear();
  });

  it('registers all built-in and advanced control types and supports custom extension', () => {
    const types = FormControlRegistry.getRegisteredTypes();
    expect(types).toEqual(
      expect.arrayContaining([
        'text',
        'textarea',
        'number',
        'currency',
        'date',
        'select',
        'checkbox',
        'repeater',
        'file',
        'peoplePicker',
        'signature',
        'lookup'
      ])
    );

    FormControlRegistry.register('customRating', ({ field }) => (
      <div data-testid={`custom-${field.name}`}>Custom Control</div>
    ));
    expect(FormControlRegistry.has('customRating')).toBe(true);
  });

  it('renders peoplePicker, signature, lookup, and select inside repeater', async () => {
    const form: SmartFormDefinition = {
      id: 'form-c3-controls',
      name: 'C3ControlsForm',
      title: 'Advanced Controls Test',
      description: 'Tests C.3 controls',
      category: 'IT',
      status: 'Published',
      submissionsCount: 0,
      updatedAt: new Date().toISOString(),
      fields: [
        {
          id: 'fld-approver',
          name: 'Approver',
          label: 'Approver',
          type: 'peoplePicker',
          required: true,
          readOnly: false,
          width: 'half'
        },
        {
          id: 'fld-vendor',
          name: 'VendorLookup',
          label: 'Vendor Catalog',
          type: 'lookup',
          required: false,
          readOnly: false,
          width: 'half',
          lookupSmartObjectId: 'so-expense',
          lookupValueField: 'RequestID',
          lookupDisplayField: 'VendorName'
        },
        {
          id: 'fld-items',
          name: 'LineItems',
          label: 'Line Items',
          type: 'repeater',
          required: false,
          readOnly: false,
          width: 'full',
          repeaterColumns: [
            { name: 'Item', label: 'Item', type: 'text', required: true },
            { name: 'Category', label: 'Category', type: 'select', options: ['Hardware', 'Software', 'Travel'] }
          ]
        },
        {
          id: 'fld-sig',
          name: 'RequesterSignature',
          label: 'Requester Signature',
          type: 'signature',
          required: false,
          readOnly: false,
          width: 'full'
        }
      ]
    };

    render(<SmartFormRenderer form={form} />);

    // 1. People Picker search & select
    const pickerInput = screen.getByLabelText(/Approver/i);
    fireEvent.focus(pickerInput);
    fireEvent.change(pickerInput, { target: { value: 'Adel' } });
    await waitFor(() => {
      expect(screen.getByText('Adel Sebti')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByText('Adel Sebti'));
    expect(screen.getByText(/adel\.sebti@corp\.internal/i)).toBeInTheDocument();

    // 2. Async Lookup search & select
    const lookupInput = screen.getByLabelText(/Vendor Catalog/i);
    fireEvent.focus(lookupInput);
    fireEvent.change(lookupInput, { target: { value: 'Dell' } });
    await waitFor(() => {
      expect(screen.getByText('Dell Technologies')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByText('Dell Technologies'));
    expect(screen.getByText('Dell Technologies')).toBeInTheDocument();

    // 3. Repeater with select column
    fireEvent.click(screen.getByRole('button', { name: /Add Line Item/i }));
    const categorySelect = screen.getByLabelText(/Category row 1/i) as HTMLSelectElement;
    fireEvent.change(categorySelect, { target: { value: 'Hardware' } });
    expect(categorySelect.value).toBe('Hardware');

    // 4. Signature pad canvas present
    expect(screen.getByTestId('signature-canvas-RequesterSignature')).toBeInTheDocument();
  });

  it('allows end-user to browse Service Catalog, save a draft, and resume it in ServicePortalView', async () => {
    useSmartFormStore.setState({ mySubmissions: [] });

    render(<ServicePortalView />);

    // Service Catalog shows published forms
    expect(screen.getByText('Executive Purchase Requisition')).toBeInTheDocument();

    // Start Request opens the form modal
    const startButtons = screen.getAllByRole('button', { name: /Start Request/i });
    fireEvent.click(startButtons[0]);

    // Fill partial data and save draft
    const vendorInput = screen.getByLabelText(/Vendor Name/i);
    fireEvent.change(vendorInput, { target: { value: 'Draft Vendor Corp' } });

    fireEvent.click(screen.getByRole('button', { name: /Save Draft/i }));

    await waitFor(() => {
      expect(screen.getByText(/Draft saved for/i)).toBeInTheDocument();
    });

    // Close modal and switch to My Requests & Drafts tab
    fireEvent.click(screen.getByRole('button', { name: /Close request form/i }));
    fireEvent.click(screen.getByRole('button', { name: /My Requests & Drafts/i }));

    // Draft is listed and can be resumed
    expect(screen.getByRole('button', { name: /Resume Draft/i })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Resume Draft/i }));

    const resumedVendorInput = screen.getByLabelText(/Vendor Name/i) as HTMLInputElement;
    expect(resumedVendorInput.value).toBe('Draft Vendor Corp');
  });
});
