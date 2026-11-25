import { create } from 'zustand';
import type {
  SmartFormDefinition,
  SmartFormField,
  SmartFormSubmission,
  FormAttachmentValue
} from '../types/smartform';
import type { SmartObjectEntity } from '../types/workflow';
import { getWorkflowRepository, repositoryManager } from '../services/repositoryManager';

const DEMO_FORMS_KEY = 'tamam_smartforms_v1';
const DEMO_SUBMISSIONS_KEY = 'tamam_smartform_submissions_v1';

const INITIAL_SMART_FORMS: SmartFormDefinition[] = [
  {
    id: 'form-purchase-req',
    name: 'PurchaseRequisitionForm',
    title: 'Executive Purchase Requisition',
    description: 'Procurement request form bound to PurchaseRequest SmartObject with conditional ROI justification, repeatable line items, and vendor quote PDF upload.',
    category: 'Finance',
    linkedSmartObjectId: 'so-expense',
    linkedWorkflowKey: 'expense',
    status: 'Published',
    submissionsCount: 2,
    updatedAt: new Date().toISOString(),
    fields: [
      {
        id: 'sec-1',
        name: 'VendorSection',
        label: 'Vendor & Cost Center Information',
        type: 'section',
        required: false,
        readOnly: false,
        width: 'full',
        helpText: 'Specify supplier details and target budget department.'
      },
      {
        id: 'fld-vendor',
        name: 'VendorName',
        label: 'Vendor Name',
        type: 'text',
        required: true,
        readOnly: false,
        width: 'half',
        placeholder: 'e.g. Dell Technologies'
      },
      {
        id: 'fld-dept',
        name: 'Department',
        label: 'Cost Center Department',
        type: 'select',
        required: true,
        readOnly: false,
        width: 'half',
        options: ['Engineering', 'Finance', 'IT Security', 'Operations', 'HR']
      },
      {
        id: 'fld-amount',
        name: 'Amount',
        label: 'Total Purchase Amount ($)',
        type: 'currency',
        required: true,
        readOnly: false,
        width: 'half',
        defaultValue: '4500'
      },
      {
        id: 'fld-date',
        name: 'NeededByDate',
        label: 'Required Delivery Date',
        type: 'date',
        required: true,
        readOnly: false,
        width: 'half'
      },
      {
        id: 'fld-exec-just',
        name: 'ExecutiveJustification',
        label: 'VP Executive Justification (Required when Amount > $5,000)',
        type: 'textarea',
        required: false,
        readOnly: false,
        width: 'full',
        placeholder: 'Provide business ROI and executive sponsorship details...',
        visibilityRule: {
          field: 'Amount',
          operator: 'greater_than',
          value: '5000'
        }
      },
      {
        id: 'fld-items',
        name: 'LineItems',
        label: 'Purchase Line Items',
        type: 'repeater',
        required: false,
        readOnly: false,
        width: 'full',
        repeaterColumns: [
          { name: 'Item', label: 'Item Description', type: 'text', required: true },
          { name: 'Qty', label: 'Quantity', type: 'number', required: true },
          { name: 'UnitPrice', label: 'Unit Price ($)', type: 'number', required: true }
        ]
      },
      {
        id: 'fld-quote',
        name: 'VendorQuote',
        label: 'Official Vendor Quote (PDF / Image)',
        type: 'file',
        required: false,
        readOnly: false,
        width: 'full',
        helpText: 'Attach PDF quote or invoice up to 10 MB.'
      }
    ]
  },
  {
    id: 'form-onboarding',
    name: 'EmployeeOnboardingForm',
    title: 'New Employee Onboarding Form',
    description: 'HR requisition form linked to Employee SmartObject and Employee Onboarding Workflow.',
    category: 'HR',
    linkedSmartObjectId: 'so-employee',
    linkedWorkflowKey: 'onboarding',
    status: 'Published',
    submissionsCount: 1,
    updatedAt: new Date().toISOString(),
    fields: [
      {
        id: 'onb-name',
        name: 'FullName',
        label: 'Employee Full Name',
        type: 'text',
        required: true,
        readOnly: false,
        width: 'half',
        placeholder: 'e.g. Sofiane Haddad'
      },
      {
        id: 'onb-dept',
        name: 'Department',
        label: 'Target Department',
        type: 'select',
        required: true,
        readOnly: false,
        width: 'half',
        options: ['Engineering', 'Finance', 'Operations', 'IT', 'Legal', 'HR']
      },
      {
        id: 'onb-salary',
        name: 'Salary',
        label: 'Annual Compensation ($)',
        type: 'currency',
        required: true,
        readOnly: false,
        width: 'half',
        defaultValue: '95000'
      },
      {
        id: 'onb-date',
        name: 'HireDate',
        label: 'Start Date',
        type: 'date',
        required: true,
        readOnly: false,
        width: 'half'
      },
      {
        id: 'onb-remote',
        name: 'RequiresVpnHardware',
        label: 'Provision Remote VPN & Encrypted Laptop',
        type: 'checkbox',
        required: false,
        readOnly: false,
        width: 'full',
        defaultValue: 'true'
      }
    ]
  }
];

function loadDemoForms(): SmartFormDefinition[] {
  try {
    const raw = localStorage.getItem(DEMO_FORMS_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      if (Array.isArray(parsed) && parsed.length > 0) return parsed;
    }
  } catch {
    // ignore
  }
  return INITIAL_SMART_FORMS;
}

function saveDemoForms(forms: SmartFormDefinition[]): void {
  try {
    localStorage.setItem(DEMO_FORMS_KEY, JSON.stringify(forms));
  } catch {
    // ignore
  }
}

function getApiConfig() {
  const baseUrl = (import.meta.env.VITE_API_BASE_URL as string) || 'http://localhost:5000/api';
  const session = getWorkflowRepository().getAuthSession();
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (session?.token) {
    headers['Authorization'] = `Bearer ${session.token}`;
  }
  return { baseUrl, headers, session };
}

/** Generates a fresh Idempotency-Key (UUID v4) for one logical submission attempt. */
export function generateIdempotencyKey(): string {
  const c = (globalThis as { crypto?: Crypto }).crypto;
  if (c && typeof c.randomUUID === 'function') {
    return c.randomUUID();
  }
  return `idem-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}

function formatHumanLabel(identifier: string): string {
  if (!identifier) return 'Field';
  return identifier
    .replace(/_/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/^./, (s) => s.toUpperCase());
}

interface SmartFormStoreState {
  forms: SmartFormDefinition[];
  selectedFormId: string;
  selectedFieldId: string | null;
  submissions: Record<string, SmartFormSubmission[]>;
  isLoading: boolean;

  syncForms: () => Promise<void>;
  selectForm: (formId: string) => void;
  selectField: (fieldId: string | null) => void;
  createForm: (partial: Pick<SmartFormDefinition, 'name' | 'title' | 'description' | 'category' | 'linkedSmartObjectId' | 'linkedWorkflowKey'>) => Promise<SmartFormDefinition>;
  saveForm: (form: SmartFormDefinition) => Promise<void>;
  deleteForm: (formId: string) => Promise<void>;
  generateFromSmartObject: (smartObject: SmartObjectEntity, linkedWorkflowKey?: string) => Promise<SmartFormDefinition>;

  addFieldToActiveForm: (field: SmartFormField) => void;
  updateFieldInActiveForm: (fieldId: string, updates: Partial<SmartFormField>) => void;
  removeFieldFromActiveForm: (fieldId: string) => void;
  moveFieldInActiveForm: (fromIndex: number, toIndex: number) => void;
  updateActiveFormMeta: (updates: Partial<SmartFormDefinition>) => void;

  uploadAttachment: (file: File) => Promise<FormAttachmentValue>;
  downloadAttachment: (attachment: FormAttachmentValue) => Promise<void>;
  submitForm: (formId: string, data: Record<string, unknown>, idempotencyKey?: string) => Promise<SmartFormSubmission>;
}

const initialForms = loadDemoForms();

export const useSmartFormStore = create<SmartFormStoreState>((set, get) => ({
  forms: initialForms,
  selectedFormId: initialForms[0]?.id || '',
  selectedFieldId: initialForms[0]?.fields[0]?.id || null,
  submissions: {},
  isLoading: false,

  syncForms: async () => {
    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      set({ isLoading: true });
      try {
        const res = await fetch(`${baseUrl}/smartforms`, { headers });
        if (res.ok) {
          const remoteForms: SmartFormDefinition[] = await res.json();
          if (Array.isArray(remoteForms) && remoteForms.length > 0) {
            set((state) => ({
              forms: remoteForms,
              selectedFormId: remoteForms.some((f) => f.id === state.selectedFormId)
                ? state.selectedFormId
                : remoteForms[0].id,
              isLoading: false
            }));
            return;
          }
        }
      } catch {
        // Fallback to local
      }
      set({ isLoading: false });
    } else {
      const demo = loadDemoForms();
      set((state) => ({
        forms: demo,
        selectedFormId: demo.some((f) => f.id === state.selectedFormId)
          ? state.selectedFormId
          : (demo[0]?.id || '')
      }));
    }
  },

  selectForm: (formId) => {
    const target = get().forms.find((f) => f.id === formId);
    set({
      selectedFormId: formId,
      selectedFieldId: target?.fields[0]?.id || null
    });
  },

  selectField: (fieldId) => set({ selectedFieldId: fieldId }),

  createForm: async (partial) => {
    const newForm: SmartFormDefinition = {
      id: `form-${Date.now().toString(36)}`,
      name: partial.name.replace(/\s+/g, ''),
      title: partial.title || partial.name,
      description: partial.description || '',
      category: partial.category || 'General',
      linkedSmartObjectId: partial.linkedSmartObjectId || null,
      linkedWorkflowKey: partial.linkedWorkflowKey || null,
      status: 'Published',
      submissionsCount: 0,
      updatedAt: new Date().toISOString(),
      fields: [
        {
          id: `fld-${Date.now()}`,
          name: 'RequesterNotes',
          label: 'Request Summary',
          type: 'text',
          required: true,
          readOnly: false,
          width: 'full',
          placeholder: 'Enter summary...'
        }
      ]
    };

    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      try {
        const res = await fetch(`${baseUrl}/smartforms`, {
          method: 'POST',
          headers,
          body: JSON.stringify(newForm)
        });
        if (res.ok) {
          const created: SmartFormDefinition = await res.json();
          set((s) => ({
            forms: [created, ...s.forms],
            selectedFormId: created.id,
            selectedFieldId: created.fields[0]?.id || null
          }));
          return created;
        }
      } catch {
        // fallback below
      }
    }

    const updated = [newForm, ...get().forms];
    saveDemoForms(updated);
    set({
      forms: updated,
      selectedFormId: newForm.id,
      selectedFieldId: newForm.fields[0]?.id || null
    });
    return newForm;
  },

  saveForm: async (form) => {
    let stamped: SmartFormDefinition = { ...form, updatedAt: new Date().toISOString() };
    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      let res: Response | null = null;
      try {
        // Update an existing form with PUT + optimistic-concurrency token
        res = await fetch(`${baseUrl}/smartforms/${encodeURIComponent(stamped.id)}`, {
          method: 'PUT',
          headers,
          body: JSON.stringify({ ...stamped, expectedVersion: stamped.version })
        });
        if (res.status === 404) {
          // Form not yet known server-side: create it
          res = await fetch(`${baseUrl}/smartforms`, {
            method: 'POST',
            headers,
            body: JSON.stringify(stamped)
          });
        }
      } catch {
        res = null;
      }

      if (res?.status === 409) {
        throw new Error(
          'CONCURRENT_MODIFICATION: this form was modified by another user. Reload it and re-apply your changes.'
        );
      }

      if (res?.ok) {
        try {
          const saved = await res.json();
          if (typeof saved?.version === 'number') {
            stamped = { ...stamped, version: saved.version };
          }
        } catch {
          // keep local copy when the body cannot be parsed
        }
      }
    }

    const updated = get().forms.map((f) => (f.id === stamped.id ? stamped : f));
    saveDemoForms(updated);
    set({ forms: updated });
  },

  deleteForm: async (formId) => {
    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      try {
        await fetch(`${baseUrl}/smartforms/${encodeURIComponent(formId)}`, {
          method: 'DELETE',
          headers
        });
      } catch {
        // ignore
      }
    }

    const remaining = get().forms.filter((f) => f.id !== formId);
    saveDemoForms(remaining);
    set({
      forms: remaining,
      selectedFormId: remaining[0]?.id || '',
      selectedFieldId: remaining[0]?.fields[0]?.id || null
    });
  },

  generateFromSmartObject: async (smartObject, linkedWorkflowKey) => {
    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      try {
        const q = linkedWorkflowKey ? `?workflowKey=${encodeURIComponent(linkedWorkflowKey)}` : '';
        const res = await fetch(`${baseUrl}/smartforms/generate/${encodeURIComponent(smartObject.id)}${q}`, {
          method: 'POST',
          headers
        });
        if (res.ok) {
          const generated: SmartFormDefinition = await res.json();
          set((s) => {
            const others = s.forms.filter((f) => f.id !== generated.id);
            return {
              forms: [generated, ...others],
              selectedFormId: generated.id,
              selectedFieldId: generated.fields[0]?.id || null
            };
          });
          return generated;
        }
      } catch {
        // client-side fallback below
      }
    }

    const generatedFields: SmartFormField[] = smartObject.fields.map((sf, idx) => {
      const lower = sf.type.toLowerCase();
      let type: SmartFormField['type'] = 'text';
      if (lower === 'number') {
        type = /amount|salary|price|cost|total/i.test(sf.name) ? 'currency' : 'number';
      } else if (lower === 'boolean') {
        type = 'checkbox';
      } else if (lower === 'datetime') {
        type = 'date';
      }

      return {
        id: `gen-${idx + 1}-${Date.now()}`,
        name: sf.name,
        label: formatHumanLabel(sf.name),
        type,
        required: Boolean(sf.isRequired && !sf.isPrimaryKey),
        readOnly: Boolean(sf.isPrimaryKey),
        width: 'half',
        placeholder: sf.isPrimaryKey ? 'Auto-generated Primary Key' : `Enter ${formatHumanLabel(sf.name)}...`
      };
    });

    const generatedForm: SmartFormDefinition = {
      id: `form-${smartObject.id}`,
      name: `${smartObject.name}Form`,
      title: `${smartObject.displayName} Form`,
      description: `Auto-generated No-Code SmartForm bound to ${smartObject.displayName} (${smartObject.name}).`,
      category: smartObject.category || 'Custom',
      linkedSmartObjectId: smartObject.id,
      linkedWorkflowKey: linkedWorkflowKey || null,
      status: 'Published',
      submissionsCount: 0,
      updatedAt: new Date().toISOString(),
      fields: generatedFields
    };

    const others = get().forms.filter((f) => f.id !== generatedForm.id);
    const nextForms = [generatedForm, ...others];
    saveDemoForms(nextForms);
    set({
      forms: nextForms,
      selectedFormId: generatedForm.id,
      selectedFieldId: generatedFields[0]?.id || null
    });
    return generatedForm;
  },

  addFieldToActiveForm: (field) => {
    const { selectedFormId, forms } = get();
    const updated = forms.map((f) =>
      f.id === selectedFormId
        ? { ...f, fields: [...f.fields, field], updatedAt: new Date().toISOString() }
        : f
    );
    saveDemoForms(updated);
    set({ forms: updated, selectedFieldId: field.id });
  },

  updateFieldInActiveForm: (fieldId, updates) => {
    const { selectedFormId, forms } = get();
    const updated = forms.map((f) =>
      f.id === selectedFormId
        ? {
            ...f,
            fields: f.fields.map((fld) => (fld.id === fieldId ? { ...fld, ...updates } : fld)),
            updatedAt: new Date().toISOString()
          }
        : f
    );
    saveDemoForms(updated);
    set({ forms: updated });
  },

  removeFieldFromActiveForm: (fieldId) => {
    const { selectedFormId, forms, selectedFieldId } = get();
    const updated = forms.map((f) =>
      f.id === selectedFormId
        ? {
            ...f,
            fields: f.fields.filter((fld) => fld.id !== fieldId),
            updatedAt: new Date().toISOString()
          }
        : f
    );
    saveDemoForms(updated);
    const active = updated.find((f) => f.id === selectedFormId);
    set({
      forms: updated,
      selectedFieldId: selectedFieldId === fieldId ? (active?.fields[0]?.id || null) : selectedFieldId
    });
  },

  moveFieldInActiveForm: (fromIndex, toIndex) => {
    const { selectedFormId, forms } = get();
    const active = forms.find((f) => f.id === selectedFormId);
    if (!active || toIndex < 0 || toIndex >= active.fields.length) return;

    const nextFields = [...active.fields];
    const [moved] = nextFields.splice(fromIndex, 1);
    nextFields.splice(toIndex, 0, moved);

    const updated = forms.map((f) =>
      f.id === selectedFormId ? { ...f, fields: nextFields, updatedAt: new Date().toISOString() } : f
    );
    saveDemoForms(updated);
    set({ forms: updated });
  },

  updateActiveFormMeta: (updates) => {
    const { selectedFormId, forms } = get();
    const updated = forms.map((f) =>
      f.id === selectedFormId ? { ...f, ...updates, updatedAt: new Date().toISOString() } : f
    );
    saveDemoForms(updated);
    set({ forms: updated });
  },

  uploadAttachment: async (file) => {
    const dataUrl = await new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result || ''));
      reader.onerror = () => reject(new Error('Failed to read file'));
      reader.readAsDataURL(file);
    });

    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      const res = await fetch(`${baseUrl}/attachments/upload`, {
        method: 'POST',
        headers,
        body: JSON.stringify({
          fileName: file.name,
          contentType: file.type || 'application/octet-stream',
          base64Content: dataUrl
        })
      });
      if (!res.ok) {
        const errText = await res.text();
        let cleanMessage = errText;
        try {
          const parsed = JSON.parse(errText);
          if (parsed?.error) {
            cleanMessage = parsed.error;
          }
        } catch {
          // keep raw error text
        }
        throw new Error(`Upload rejected (${res.status}): ${cleanMessage}`);
      }
      const meta = await res.json();
      return {
        id: meta.id,
        fileName: meta.fileName,
        contentType: meta.contentType,
        sizeBytes: meta.sizeBytes,
        sha256Hash: meta.sha256Hash,
        downloadUrl: `${baseUrl.replace(/\/api$/, '')}${meta.downloadUrl}`,
        previewDataUrl: dataUrl
      };
    }

    // Demo mode validation (enforce 10MB limit & strict allowed extensions)
    if (file.size > 10 * 1024 * 1024) {
      throw new Error('File exceeds maximum allowed size of 10 MB.');
    }
    if (!/\.(pdf|png|jpe?g|docx|xlsx)$/i.test(file.name)) {
      throw new Error('INVALID_FILE_TYPE: Only PDF, PNG, JPEG, DOCX, and XLSX files are allowed.');
    }

    return {
      id: `att-${Date.now().toString(36)}`,
      fileName: file.name,
      contentType: file.type || 'application/octet-stream',
      sizeBytes: file.size,
      sha256Hash: 'demo-sha256-verified',
      previewDataUrl: dataUrl
    };
  },

  downloadAttachment: async (attachment) => {
    if (repositoryManager.getMode() === 'connected' && attachment.downloadUrl) {
      const { session } = getApiConfig();
      const headers: Record<string, string> = {};
      if (session?.token) {
        headers['Authorization'] = `Bearer ${session.token}`;
      }

      const res = await fetch(attachment.downloadUrl, { headers });
      if (!res.ok) {
        const errText = await res.text();
        throw new Error(
          res.status === 403
            ? 'Access denied (403): You are not authorized to download this attachment.'
            : `Download failed (${res.status}): ${errText}`
        );
      }

      const blob = await res.blob();
      const objectUrl = URL.createObjectURL(blob);
      try {
        const link = document.createElement('a');
        link.href = objectUrl;
        link.download = attachment.fileName || 'attachment';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
      } finally {
        URL.revokeObjectURL(objectUrl);
      }
      return;
    }

    if (attachment.previewDataUrl) {
      const link = document.createElement('a');
      link.href = attachment.previewDataUrl;
      link.download = attachment.fileName || 'attachment';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    }
  },

  submitForm: async (formId, data, idempotencyKey) => {
    const form = get().forms.find((f) => f.id === formId);
    if (!form) throw new Error(`Form '${formId}' not found.`);

    const repo = getWorkflowRepository();
    const userEmail = repo.getAuthSession()?.user.email || 'mohamed.fetni@enterprise.com';

    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      const res = await fetch(`${baseUrl}/smartforms/${encodeURIComponent(formId)}/submit`, {
        method: 'POST',
        headers: { ...headers, 'Idempotency-Key': idempotencyKey || generateIdempotencyKey() },
        body: JSON.stringify({ data, startLinkedWorkflow: true })
      });
      if (res.ok) {
        const sub: SmartFormSubmission = await res.json();
        set((s) => ({
          forms: s.forms.map((f) =>
            f.id === formId ? { ...f, submissionsCount: f.submissionsCount + 1 } : f
          ),
          submissions: {
            ...s.submissions,
            [formId]: [sub, ...(s.submissions[formId] || [])]
          }
        }));
        return sub;
      }

      if (res.status >= 400 && res.status < 500) {
        const errBody = await res.text();
        let message = `Form validation failed (${res.status}).`;
        let fieldErrors: Record<string, string> | undefined;
        try {
          const parsed = JSON.parse(errBody);
          if (parsed?.error) {
            message = parsed.error;
          } else if (parsed?.detail) {
            message = parsed.detail;
          }
          if (parsed?.errors && typeof parsed.errors === 'object') {
            fieldErrors = {};
            for (const [k, msgs] of Object.entries(parsed.errors)) {
              if (Array.isArray(msgs) && msgs.length > 0) {
                fieldErrors[k] = String(msgs[0]);
              }
            }
          }
        } catch {
          if (errBody) message = errBody;
        }
        const validationErr = new Error(message) as Error & { fieldErrors?: Record<string, string> };
        validationErr.fieldErrors = fieldErrors;
        throw validationErr;
      }
    }

    // Demo Mode: persist record to SmartObject + start WorkflowInstance if linked
    if (form.linkedSmartObjectId) {
      await repo.addSmartObjectRecord(form.linkedSmartObjectId, {
        ...data,
        SubmittedAt: new Date().toISOString().slice(0, 10)
      });
    }

    let linkedInstanceId: string | null = null;
    if (form.linkedWorkflowKey) {
      const started = await repo.startInstance(form.linkedWorkflowKey, userEmail, data);
      linkedInstanceId = started.id;
    }

    const demoSub: SmartFormSubmission = {
      id: `SUB-${Math.floor(1000 + Math.random() * 9000)}`,
      smartFormId: formId,
      submittedBy: userEmail,
      data,
      linkedInstanceId,
      submittedAt: new Date().toISOString()
    };

    const updatedForms = get().forms.map((f) =>
      f.id === formId ? { ...f, submissionsCount: f.submissionsCount + 1 } : f
    );
    saveDemoForms(updatedForms);

    try {
      const prevRaw = localStorage.getItem(DEMO_SUBMISSIONS_KEY);
      const prev = prevRaw ? JSON.parse(prevRaw) : {};
      prev[formId] = [demoSub, ...(prev[formId] || [])];
      localStorage.setItem(DEMO_SUBMISSIONS_KEY, JSON.stringify(prev));
    } catch {
      // ignore
    }

    set((s) => ({
      forms: updatedForms,
      submissions: {
        ...s.submissions,
        [formId]: [demoSub, ...(s.submissions[formId] || [])]
      }
    }));

    return demoSub;
  }
}));
