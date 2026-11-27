import { create } from 'zustand';
import type {
  SmartFormDefinition,
  SmartFormField,
  SmartFormSubmission,
  SmartFormVersionSummary,
  SmartFormVersionDetail,
  FormAttachmentValue,
  FormRule
} from '../types/smartform';
import type { SmartObjectEntity } from '../types/workflow';
import { getWorkflowRepository, repositoryManager } from '../services/repositoryManager';
import { analyzeFormRuleGraph } from '../utils/jsonLogicEngine';

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
    version: 1,
    activePublishedVersionId: 'sfv-purchase-v1',
    latestVersionNumber: 1,
    latestVersionId: 'sfv-purchase-v1',
    latestVersionStatus: 'Published',
    hasUnpublishedDraft: false,
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
    version: 1,
    activePublishedVersionId: 'sfv-onboarding-v1',
    latestVersionNumber: 1,
    latestVersionId: 'sfv-onboarding-v1',
    latestVersionStatus: 'Published',
    hasUnpublishedDraft: false,
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
  selectedVersionId: string | null;
  versionsByForm: Record<string, SmartFormVersionSummary[]>;
  versionDetails: Record<string, SmartFormVersionDetail>;
  submissions: Record<string, SmartFormSubmission[]>;
  mySubmissions: SmartFormSubmission[];
  isLoading: boolean;

  syncForms: () => Promise<void>;
  selectForm: (formId: string) => void;
  selectField: (fieldId: string | null) => void;
  selectVersion: (versionId: string | null) => void;
  fetchFormVersions: (formId: string) => Promise<SmartFormVersionSummary[]>;
  fetchFormVersion: (formId: string, versionId: string) => Promise<SmartFormVersionDetail | null>;
  publishFormVersion: (formId: string) => Promise<SmartFormDefinition>;
  createForm: (partial: Pick<SmartFormDefinition, 'name' | 'title' | 'description' | 'category' | 'linkedSmartObjectId' | 'linkedWorkflowKey'>) => Promise<SmartFormDefinition>;
  saveForm: (form: SmartFormDefinition) => Promise<void>;
  deleteForm: (formId: string) => Promise<void>;
  generateFromSmartObject: (smartObject: SmartObjectEntity, linkedWorkflowKey?: string) => Promise<SmartFormDefinition>;

  addFieldToActiveForm: (field: SmartFormField) => void;
  updateFieldInActiveForm: (fieldId: string, updates: Partial<SmartFormField>) => void;
  removeFieldFromActiveForm: (fieldId: string) => void;
  moveFieldInActiveForm: (fromIndex: number, toIndex: number) => void;
  updateActiveFormMeta: (updates: Partial<SmartFormDefinition>) => void;
  addRuleToActiveForm: (rule: FormRule) => void;
  updateRuleInActiveForm: (ruleId: string, updates: Partial<FormRule>) => void;
  removeRuleFromActiveForm: (ruleId: string) => void;

  uploadAttachment: (file: File) => Promise<FormAttachmentValue>;
  downloadAttachment: (attachment: FormAttachmentValue) => Promise<void>;
  fetchMySubmissions: () => Promise<SmartFormSubmission[]>;
  saveDraft: (formId: string, data: Record<string, unknown>, draftId?: string) => Promise<SmartFormSubmission>;
  submitForm: (formId: string, data: Record<string, unknown>, idempotencyKey?: string, draftId?: string) => Promise<SmartFormSubmission>;
}

const initialForms = loadDemoForms();

function buildInitialDemoVersions(forms: SmartFormDefinition[]): {
  versionsByForm: Record<string, SmartFormVersionSummary[]>;
  versionDetails: Record<string, SmartFormVersionDetail>;
} {
  const versionsByForm: Record<string, SmartFormVersionSummary[]> = {};
  const versionDetails: Record<string, SmartFormVersionDetail> = {};
  for (const f of forms) {
    const versionId = f.latestVersionId || f.activePublishedVersionId || `sfv-${f.id}-v1`;
    const versionNumber = f.latestVersionNumber || 1;
    const status = f.latestVersionStatus || (f.status === 'Published' ? 'Published' : 'Draft');
    const detail: SmartFormVersionDetail = {
      id: versionId,
      smartFormId: f.id,
      versionNumber,
      status,
      fields: f.fields,
      publishedAt: status === 'Published' ? f.updatedAt : null,
      publishedBy: status === 'Published' ? 'system' : null,
      createdAt: f.updatedAt
    };
    versionsByForm[f.id] = [detail];
    versionDetails[`${f.id}:${versionId}`] = detail;
  }
  return { versionsByForm, versionDetails };
}

const initialVersionState = buildInitialDemoVersions(initialForms);

export const useSmartFormStore = create<SmartFormStoreState>((set, get) => ({
  forms: initialForms,
  selectedFormId: initialForms[0]?.id || '',
  selectedFieldId: initialForms[0]?.fields[0]?.id || null,
  selectedVersionId: null,
  versionsByForm: initialVersionState.versionsByForm,
  versionDetails: initialVersionState.versionDetails,
  submissions: {},
  mySubmissions: [],
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
      selectedFieldId: target?.fields[0]?.id || null,
      selectedVersionId: null
    });
    void get().fetchFormVersions(formId);
  },

  selectField: (fieldId) => set({ selectedFieldId: fieldId }),

  selectVersion: (versionId) => {
    const { selectedFormId } = get();
    set({ selectedVersionId: versionId });
    if (versionId && selectedFormId) {
      void get().fetchFormVersion(selectedFormId, versionId);
    }
  },

  fetchFormVersions: async (formId) => {
    if (!formId) return [];
    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      try {
        const res = await fetch(`${baseUrl}/smartforms/${encodeURIComponent(formId)}/versions`, { headers });
        if (res.ok) {
          const list: SmartFormVersionSummary[] = await res.json();
          set((s) => ({
            versionsByForm: { ...s.versionsByForm, [formId]: list }
          }));
          return list;
        }
      } catch {
        // fallback to local cache
      }
    }

    const existing = get().versionsByForm[formId];
    if (existing && existing.length > 0) return existing;

    const form = get().forms.find((f) => f.id === formId);
    if (!form) return [];

    const versionId = form.latestVersionId || form.activePublishedVersionId || `sfv-${form.id}-v${form.latestVersionNumber || 1}`;
    const status = form.latestVersionStatus || (form.status === 'Published' ? 'Published' : 'Draft');
    const summary: SmartFormVersionDetail = {
      id: versionId,
      smartFormId: form.id,
      versionNumber: form.latestVersionNumber || 1,
      status,
      fields: form.draftFields ?? form.fields,
      publishedAt: status === 'Published' ? form.updatedAt : null,
      publishedBy: status === 'Published' ? 'system' : null,
      createdAt: form.updatedAt
    };
    set((s) => ({
      versionsByForm: { ...s.versionsByForm, [formId]: [summary] },
      versionDetails: { ...s.versionDetails, [`${formId}:${versionId}`]: summary }
    }));
    return [summary];
  },

  fetchFormVersion: async (formId, versionId) => {
    if (!formId || !versionId) return null;
    const cacheKey = `${formId}:${versionId}`;
    const cached = get().versionDetails[cacheKey];
    if (cached && cached.status !== 'Draft') {
      return cached;
    }

    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      try {
        const res = await fetch(
          `${baseUrl}/smartforms/${encodeURIComponent(formId)}/versions/${encodeURIComponent(versionId)}`,
          { headers }
        );
        if (res.ok) {
          const detail: SmartFormVersionDetail = await res.json();
          set((s) => ({
            versionDetails: { ...s.versionDetails, [cacheKey]: detail }
          }));
          return detail;
        }
      } catch {
        // fallback to cache
      }
    }

    return cached ?? null;
  },

  publishFormVersion: async (formId) => {
    const current = get().forms.find((f) => f.id === formId);
    if (!current) {
      throw new Error(`Form '${formId}' not found.`);
    }

    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      const res = await fetch(`${baseUrl}/smartforms/${encodeURIComponent(formId)}/publish`, {
        method: 'POST',
        headers
      });
      if (!res.ok) {
        const errBody = await res.text();
        let message = `Failed to publish form (${res.status}).`;
        try {
          const parsed = JSON.parse(errBody);
          if (parsed?.error) message = parsed.error;
        } catch {
          if (errBody) message = errBody;
        }
        throw new Error(message);
      }
      const published: SmartFormDefinition = await res.json();
      set((s) => ({
        forms: s.forms.map((f) => (f.id === formId ? published : f)),
        selectedVersionId: null
      }));
      await get().fetchFormVersions(formId);
      return published;
    }

    // Demo mode publish: archive previous Published version and activate current Draft
    const now = new Date().toISOString();
    const existingVersions = get().versionsByForm[formId] || [];
    const nextVersionNumber = current.latestVersionNumber || 1;
    const activeDraft = existingVersions.find((v) => v.status === 'Draft');
    const publishedVersionId = activeDraft?.id || `sfv-${formId}-v${nextVersionNumber}`;
    const publishedFields = current.draftFields ?? current.fields;

    const updatedVersions: SmartFormVersionSummary[] = existingVersions.map((v) => {
      if (v.id === publishedVersionId) {
        return { ...v, status: 'Published', publishedAt: now, publishedBy: 'designer@enterprise.internal' };
      }
      if (v.status === 'Published') {
        return { ...v, status: 'Archived' };
      }
      return v;
    });

    if (!updatedVersions.some((v) => v.id === publishedVersionId)) {
      updatedVersions.unshift({
        id: publishedVersionId,
        smartFormId: formId,
        versionNumber: nextVersionNumber,
        status: 'Published',
        publishedAt: now,
        publishedBy: 'designer@enterprise.internal',
        createdAt: now
      });
    }

    const publishedDetail: SmartFormVersionDetail = {
      id: publishedVersionId,
      smartFormId: formId,
      versionNumber: nextVersionNumber,
      status: 'Published',
      fields: publishedFields,
      publishedAt: now,
      publishedBy: 'designer@enterprise.internal',
      createdAt: activeDraft?.createdAt || now
    };

    const publishedForm: SmartFormDefinition = {
      ...current,
      status: 'Published',
      fields: publishedFields,
      draftFields: null,
      activePublishedVersionId: publishedVersionId,
      latestVersionId: publishedVersionId,
      latestVersionNumber: nextVersionNumber,
      latestVersionStatus: 'Published',
      hasUnpublishedDraft: false,
      version: (current.version || 1) + 1,
      updatedAt: now
    };

    const updatedForms = get().forms.map((f) => (f.id === formId ? publishedForm : f));
    saveDemoForms(updatedForms);
    set((s) => ({
      forms: updatedForms,
      selectedVersionId: null,
      versionsByForm: { ...s.versionsByForm, [formId]: updatedVersions },
      versionDetails: { ...s.versionDetails, [`${formId}:${publishedVersionId}`]: publishedDetail }
    }));
    return publishedForm;
  },

  createForm: async (partial) => {
    const now = new Date().toISOString();
    const formId = `form-${Date.now().toString(36)}`;
    const draftVersionId = `sfv-${formId}-v1`;
    const newForm: SmartFormDefinition = {
      id: formId,
      name: partial.name.replace(/\s+/g, ''),
      title: partial.title || partial.name,
      description: partial.description || '',
      category: partial.category || 'General',
      linkedSmartObjectId: partial.linkedSmartObjectId || null,
      linkedWorkflowKey: partial.linkedWorkflowKey || null,
      status: 'Draft',
      submissionsCount: 0,
      updatedAt: now,
      version: 1,
      activePublishedVersionId: null,
      latestVersionNumber: 1,
      latestVersionId: draftVersionId,
      latestVersionStatus: 'Draft',
      hasUnpublishedDraft: true,
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
            selectedFieldId: created.fields[0]?.id || null,
            selectedVersionId: null
          }));
          void get().fetchFormVersions(created.id);
          return created;
        }
      } catch {
        // fallback below
      }
    }

    const v1Detail: SmartFormVersionDetail = {
      id: draftVersionId,
      smartFormId: formId,
      versionNumber: 1,
      status: 'Draft',
      fields: newForm.fields,
      publishedAt: null,
      publishedBy: null,
      createdAt: now
    };

    const updated = [newForm, ...get().forms];
    saveDemoForms(updated);
    set((s) => ({
      forms: updated,
      selectedFormId: newForm.id,
      selectedFieldId: newForm.fields[0]?.id || null,
      selectedVersionId: null,
      versionsByForm: { ...s.versionsByForm, [formId]: [v1Detail] },
      versionDetails: { ...s.versionDetails, [`${formId}:${draftVersionId}`]: v1Detail }
    }));
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

      if (res?.status === 422) {
        const errBody = await res.text();
        let msg = 'CYCLIC_FORM_RULE: Circular dependency detected in form rules.';
        try {
          const parsed = JSON.parse(errBody);
          if (parsed?.error) msg = parsed.error;
        } catch {
          if (errBody) msg = errBody;
        }
        throw new Error(msg);
      }

      if (res?.ok) {
        try {
          const saved: SmartFormDefinition = await res.json();
          stamped = {
            ...stamped,
            ...saved,
            fields: saved.draftFields ?? saved.fields ?? stamped.fields,
            rules: saved.draftRules ?? saved.rules ?? stamped.rules
          };
          void get().fetchFormVersions(stamped.id);
        } catch {
          // keep local copy when the body cannot be parsed
        }
      }
    } else {
      // Demo mode: enforce acyclic DAG before saving
      const graph = analyzeFormRuleGraph(stamped.fields, stamped.rules);
      if (graph.hasCycle) {
        throw new Error(
          `CYCLIC_FORM_RULE: Circular dependency detected in form rules (${graph.cyclePath.join(' -> ')}).`
        );
      }

      // Demo mode: create a new Draft version if the latest version is Published
      const existingVersions = get().versionsByForm[stamped.id] || [];
      const existingDraft = existingVersions.find((v) => v.status === 'Draft');
      if (existingDraft) {
        const updatedDetail: SmartFormVersionDetail = {
          ...existingDraft,
          fields: stamped.fields,
          rules: stamped.rules
        };
        set((s) => ({
          versionDetails: { ...s.versionDetails, [`${stamped.id}:${existingDraft.id}`]: updatedDetail }
        }));
      } else {
        const nextNum = (stamped.latestVersionNumber || 1) + 1;
        const nextId = `sfv-${stamped.id}-v${nextNum}`;
        const newDraft: SmartFormVersionDetail = {
          id: nextId,
          smartFormId: stamped.id,
          versionNumber: nextNum,
          status: 'Draft',
          fields: stamped.fields,
          rules: stamped.rules,
          publishedAt: null,
          publishedBy: null,
          createdAt: stamped.updatedAt
        };
        stamped = {
          ...stamped,
          latestVersionNumber: nextNum,
          latestVersionId: nextId,
          latestVersionStatus: 'Draft',
          hasUnpublishedDraft: true,
          draftFields: stamped.fields,
          draftRules: stamped.rules ?? [],
          version: (stamped.version || 1) + 1
        };
        set((s) => ({
          versionsByForm: {
            ...s.versionsByForm,
            [stamped.id]: [newDraft, ...existingVersions]
          },
          versionDetails: { ...s.versionDetails, [`${stamped.id}:${nextId}`]: newDraft }
        }));
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
    const updated = forms.map((f) => {
      if (f.id !== selectedFormId) return f;
      const nextFields = [...f.fields, field];
      const alreadyDraft = f.hasUnpublishedDraft || f.latestVersionStatus === 'Draft';
      const nextNum = alreadyDraft ? (f.latestVersionNumber || 1) : (f.latestVersionNumber || 1) + 1;
      return {
        ...f,
        fields: nextFields,
        draftFields: nextFields,
        latestVersionNumber: nextNum,
        latestVersionStatus: 'Draft' as const,
        hasUnpublishedDraft: true,
        updatedAt: new Date().toISOString()
      };
    });
    saveDemoForms(updated);
    set({ forms: updated, selectedFieldId: field.id });
  },

  updateFieldInActiveForm: (fieldId, updates) => {
    const { selectedFormId, forms } = get();
    const updated = forms.map((f) => {
      if (f.id !== selectedFormId) return f;
      const nextFields = f.fields.map((fld) => (fld.id === fieldId ? { ...fld, ...updates } : fld));
      const alreadyDraft = f.hasUnpublishedDraft || f.latestVersionStatus === 'Draft';
      const nextNum = alreadyDraft ? (f.latestVersionNumber || 1) : (f.latestVersionNumber || 1) + 1;
      return {
        ...f,
        fields: nextFields,
        draftFields: nextFields,
        latestVersionNumber: nextNum,
        latestVersionStatus: 'Draft' as const,
        hasUnpublishedDraft: true,
        updatedAt: new Date().toISOString()
      };
    });
    saveDemoForms(updated);
    set({ forms: updated });
  },

  removeFieldFromActiveForm: (fieldId) => {
    const { selectedFormId, forms, selectedFieldId } = get();
    const updated = forms.map((f) => {
      if (f.id !== selectedFormId) return f;
      const nextFields = f.fields.filter((fld) => fld.id !== fieldId);
      const alreadyDraft = f.hasUnpublishedDraft || f.latestVersionStatus === 'Draft';
      const nextNum = alreadyDraft ? (f.latestVersionNumber || 1) : (f.latestVersionNumber || 1) + 1;
      return {
        ...f,
        fields: nextFields,
        draftFields: nextFields,
        latestVersionNumber: nextNum,
        latestVersionStatus: 'Draft' as const,
        hasUnpublishedDraft: true,
        updatedAt: new Date().toISOString()
      };
    });
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

  addRuleToActiveForm: (rule) => {
    const { selectedFormId, forms } = get();
    const updated = forms.map((f) => {
      if (f.id !== selectedFormId) return f;
      const nextRules = [...(f.rules || []), rule];
      const alreadyDraft = f.hasUnpublishedDraft || f.latestVersionStatus === 'Draft';
      const nextNum = alreadyDraft ? (f.latestVersionNumber || 1) : (f.latestVersionNumber || 1) + 1;
      return {
        ...f,
        rules: nextRules,
        draftRules: nextRules,
        latestVersionNumber: nextNum,
        latestVersionStatus: 'Draft' as const,
        hasUnpublishedDraft: true,
        updatedAt: new Date().toISOString()
      };
    });
    saveDemoForms(updated);
    set({ forms: updated });
  },

  updateRuleInActiveForm: (ruleId, updates) => {
    const { selectedFormId, forms } = get();
    const updated = forms.map((f) => {
      if (f.id !== selectedFormId) return f;
      const nextRules = (f.rules || []).map((r) => (r.id === ruleId ? { ...r, ...updates } : r));
      const alreadyDraft = f.hasUnpublishedDraft || f.latestVersionStatus === 'Draft';
      const nextNum = alreadyDraft ? (f.latestVersionNumber || 1) : (f.latestVersionNumber || 1) + 1;
      return {
        ...f,
        rules: nextRules,
        draftRules: nextRules,
        latestVersionNumber: nextNum,
        latestVersionStatus: 'Draft' as const,
        hasUnpublishedDraft: true,
        updatedAt: new Date().toISOString()
      };
    });
    saveDemoForms(updated);
    set({ forms: updated });
  },

  removeRuleFromActiveForm: (ruleId) => {
    const { selectedFormId, forms } = get();
    const updated = forms.map((f) => {
      if (f.id !== selectedFormId) return f;
      const nextRules = (f.rules || []).filter((r) => r.id !== ruleId);
      const alreadyDraft = f.hasUnpublishedDraft || f.latestVersionStatus === 'Draft';
      const nextNum = alreadyDraft ? (f.latestVersionNumber || 1) : (f.latestVersionNumber || 1) + 1;
      return {
        ...f,
        rules: nextRules,
        draftRules: nextRules,
        latestVersionNumber: nextNum,
        latestVersionStatus: 'Draft' as const,
        hasUnpublishedDraft: true,
        updatedAt: new Date().toISOString()
      };
    });
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

  fetchMySubmissions: async () => {
    const repo = getWorkflowRepository();
    const userEmail = repo.getAuthSession()?.user.email || 'mohamed.fetni@enterprise.com';

    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      const res = await fetch(`${baseUrl}/smartforms/my-submissions`, { headers });
      if (res.ok) {
        const list: SmartFormSubmission[] = await res.json();
        set({ mySubmissions: list });
        return list;
      }
      return get().mySubmissions;
    }

    // Demo mode: aggregate localStorage submissions + drafts for the current user
    try {
      const raw = localStorage.getItem(DEMO_SUBMISSIONS_KEY);
      const parsed: Record<string, SmartFormSubmission[]> = raw ? JSON.parse(raw) : {};
      const formsById = new Map(get().forms.map((f) => [f.id, f]));
      const all: SmartFormSubmission[] = [];
      for (const subs of Object.values(parsed)) {
        if (!Array.isArray(subs)) continue;
        for (const s of subs) {
          if (!s.submittedBy || s.submittedBy.toLowerCase() === userEmail.toLowerCase()) {
            const fm = formsById.get(s.smartFormId);
            all.push({
              ...s,
              status: s.status || 'Submitted',
              smartFormTitle: s.smartFormTitle || fm?.title || s.smartFormId,
              smartFormCategory: s.smartFormCategory || fm?.category || 'General'
            });
          }
        }
      }
      all.sort((a, b) =>
        new Date(b.updatedAt || b.submittedAt).getTime() - new Date(a.updatedAt || a.submittedAt).getTime()
      );
      set({ mySubmissions: all });
      return all;
    } catch {
      return get().mySubmissions;
    }
  },

  saveDraft: async (formId, data, draftId) => {
    const form = get().forms.find((f) => f.id === formId);
    if (!form) throw new Error(`Form '${formId}' not found.`);

    const repo = getWorkflowRepository();
    const userEmail = repo.getAuthSession()?.user.email || 'mohamed.fetni@enterprise.com';

    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      const res = await fetch(`${baseUrl}/smartforms/${encodeURIComponent(formId)}/drafts`, {
        method: 'POST',
        headers,
        body: JSON.stringify({ data, draftId: draftId || null })
      });
      if (!res.ok) {
        const errText = await res.text();
        let message = `Failed to save draft (${res.status}).`;
        try {
          const parsed = JSON.parse(errText);
          if (parsed?.error) message = parsed.error;
        } catch {
          if (errText) message = errText;
        }
        throw new Error(message);
      }
      const savedDraft: SmartFormSubmission = await res.json();
      set((s) => ({
        mySubmissions: [
          savedDraft,
          ...s.mySubmissions.filter((item) => item.id !== savedDraft.id)
        ]
      }));
      return savedDraft;
    }

    // Demo mode draft persistence
    const now = new Date().toISOString();
    const draft: SmartFormSubmission = {
      id: draftId || `drf-${Date.now().toString(36)}`,
      smartFormId: formId,
      submittedBy: userEmail,
      data,
      linkedInstanceId: null,
      submittedAt: now,
      updatedAt: now,
      status: 'Draft',
      smartFormTitle: form.title,
      smartFormCategory: form.category
    };

    try {
      const prevRaw = localStorage.getItem(DEMO_SUBMISSIONS_KEY);
      const prev: Record<string, SmartFormSubmission[]> = prevRaw ? JSON.parse(prevRaw) : {};
      const existingList = Array.isArray(prev[formId]) ? prev[formId] : [];
      prev[formId] = [draft, ...existingList.filter((item) => item.id !== draft.id)];
      localStorage.setItem(DEMO_SUBMISSIONS_KEY, JSON.stringify(prev));
    } catch {
      // ignore
    }

    set((s) => ({
      mySubmissions: [draft, ...s.mySubmissions.filter((item) => item.id !== draft.id)]
    }));
    return draft;
  },

  submitForm: async (formId, data, idempotencyKey, draftId) => {
    const form = get().forms.find((f) => f.id === formId);
    if (!form) throw new Error(`Form '${formId}' not found.`);

    const repo = getWorkflowRepository();
    const userEmail = repo.getAuthSession()?.user.email || 'mohamed.fetni@enterprise.com';

    if (repositoryManager.getMode() === 'connected') {
      const { baseUrl, headers } = getApiConfig();
      const res = await fetch(`${baseUrl}/smartforms/${encodeURIComponent(formId)}/submit`, {
        method: 'POST',
        headers: { ...headers, 'Idempotency-Key': idempotencyKey || generateIdempotencyKey() },
        body: JSON.stringify({ data, startLinkedWorkflow: true, draftId: draftId || null })
      });
      if (res.ok) {
        const sub: SmartFormSubmission = await res.json();
        set((s) => ({
          forms: s.forms.map((f) =>
            f.id === formId ? { ...f, submissionsCount: f.submissionsCount + 1 } : f
          ),
          submissions: {
            ...s.submissions,
            [formId]: [sub, ...(s.submissions[formId] || []).filter((x) => x.id !== sub.id)]
          },
          mySubmissions: [sub, ...s.mySubmissions.filter((x) => x.id !== sub.id)]
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

    const now = new Date().toISOString();
    const demoSub: SmartFormSubmission = {
      id: draftId || `SUB-${Math.floor(1000 + Math.random() * 9000)}`,
      smartFormId: formId,
      submittedBy: userEmail,
      data,
      linkedInstanceId,
      submittedAt: now,
      updatedAt: now,
      status: 'Submitted',
      smartFormTitle: form.title,
      smartFormCategory: form.category,
      instanceStatus: linkedInstanceId ? 'RUNNING' : null
    };

    const updatedForms = get().forms.map((f) =>
      f.id === formId ? { ...f, submissionsCount: f.submissionsCount + 1 } : f
    );
    saveDemoForms(updatedForms);

    try {
      const prevRaw = localStorage.getItem(DEMO_SUBMISSIONS_KEY);
      const prev = prevRaw ? JSON.parse(prevRaw) : {};
      const existingList: SmartFormSubmission[] = Array.isArray(prev[formId]) ? prev[formId] : [];
      prev[formId] = [demoSub, ...existingList.filter((x) => x.id !== demoSub.id)];
      localStorage.setItem(DEMO_SUBMISSIONS_KEY, JSON.stringify(prev));
    } catch {
      // ignore
    }

    set((s) => ({
      forms: updatedForms,
      submissions: {
        ...s.submissions,
        [formId]: [demoSub, ...(s.submissions[formId] || []).filter((x) => x.id !== demoSub.id)]
      },
      mySubmissions: [demoSub, ...s.mySubmissions.filter((x) => x.id !== demoSub.id)]
    }));

    return demoSub;
  }
}));
