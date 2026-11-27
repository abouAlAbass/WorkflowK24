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
  | 'file'
  | 'peoplePicker'
  | 'signature'
  | 'lookup';

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

export type FormRuleEffect = 'visible' | 'required' | 'readOnly' | 'computedValue';

export type JsonLogicValue =
  | string
  | number
  | boolean
  | null
  | JsonLogicValue[]
  | { [operator: string]: JsonLogicValue | JsonLogicValue[] };

export interface FormRule {
  id: string;
  targetField: string;
  effect: FormRuleEffect;
  conditionJsonLogic?: JsonLogicValue | null;
  expressionJsonLogic?: JsonLogicValue | null;
}

export interface SmartFormRepeaterColumn {
  name: string;
  label: string;
  type: 'text' | 'number' | 'date' | 'select';
  required?: boolean;
  options?: string[];
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

export type SmartFormVersionStatus = 'Draft' | 'Published' | 'Archived';

export interface SmartFormVersionSummary {
  id: string;
  smartFormId: string;
  versionNumber: number;
  status: SmartFormVersionStatus;
  publishedAt?: string | null;
  publishedBy?: string | null;
  createdAt: string;
}

export interface SmartFormVersionDetail extends SmartFormVersionSummary {
  fields: SmartFormField[];
  rules?: FormRule[];
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
  rules?: FormRule[];
  updatedAt: string;
  /** Optimistic-concurrency token returned by the API; sent back as expectedVersion on update. */
  version?: number;
  activePublishedVersionId?: string | null;
  latestVersionNumber?: number;
  latestVersionId?: string | null;
  latestVersionStatus?: SmartFormVersionStatus;
  hasUnpublishedDraft?: boolean;
  draftFields?: SmartFormField[] | null;
  draftRules?: FormRule[] | null;
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

export interface UserDirectoryItem {
  id: string;
  displayName: string;
  email: string;
  department: string;
}

export interface SmartObjectLookupPage {
  items: Record<string, unknown>[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasMore: boolean;
}

export interface SmartFormSubmission {
  id: string;
  smartFormId: string;
  submittedBy: string;
  data: Record<string, unknown>;
  linkedInstanceId?: string | null;
  submittedAt: string;
  smartFormVersionId?: string | null;
  status?: 'Draft' | 'Submitted';
  updatedAt?: string | null;
  smartFormTitle?: string | null;
  smartFormCategory?: string | null;
  instanceStatus?: string | null;
  currentStep?: string | null;
}
