export type SmartFormFieldType =
  | 'section'
  | 'text'
  | 'textarea'
  | 'number'
  | 'currency'
  | 'date'
  | 'checkbox'
  | 'select'
  | 'repeater'
  | 'file';

export type VisibilityOperator =
  | 'equals'
  | 'not_equals'
  | 'greater_than'
  | 'less_than'
  | 'contains'
  | 'is_not_empty';

export interface SmartFormVisibilityRule {
  field: string;
  operator: VisibilityOperator;
  value: string;
}

export interface SmartFormRepeaterColumn {
  name: string;
  label: string;
  type: 'text' | 'number' | 'date';
  required?: boolean;
}

export interface SmartFormField {
  id: string;
  name: string;
  label: string;
  type: SmartFormFieldType;
  required: boolean;
  readOnly: boolean;
  width: 'full' | 'half';
  placeholder?: string;
  helpText?: string;
  defaultValue?: string;
  options?: string[];
  lookupSmartObjectId?: string;
  lookupDisplayField?: string;
  lookupValueField?: string;
  repeaterColumns?: SmartFormRepeaterColumn[];
  visibilityRule?: SmartFormVisibilityRule | null;
}

export interface SmartFormDefinition {
  id: string;
  name: string;
  title: string;
  description: string;
  category: string;
  linkedSmartObjectId?: string | null;
  linkedWorkflowKey?: string | null;
  status: 'Draft' | 'Published';
  submissionsCount: number;
  fields: SmartFormField[];
  updatedAt: string;
  /** Optimistic-concurrency token returned by the API; sent back as expectedVersion on update. */
  version?: number;
}

export interface FormAttachmentValue {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256Hash?: string;
  downloadUrl?: string;
  previewDataUrl?: string;
}

export interface SmartFormSubmission {
  id: string;
  smartFormId: string;
  submittedBy: string;
  data: Record<string, unknown>;
  linkedInstanceId?: string | null;
  submittedAt: string;
}
