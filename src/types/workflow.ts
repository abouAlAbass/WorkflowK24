export type NodeType = 'trigger' | 'approval' | 'sendEmail' | 'end' | 'decision' | 'stage';

export type TriggerType = 'form' | 'schedule' | 'webhook' | 'item_created';

export interface WorkflowVariable {
  name: string;
  type: 'string' | 'number' | 'boolean' | 'date';
  defaultValue?: string;
}

export interface TriggerNodeData {
  label: string;
  triggerType: TriggerType;
  eventName: string;
  variables: WorkflowVariable[];
  description?: string;
}

export interface ApprovalOutcome {
  id: string;
  label: string;
  color: string;
}

export interface ApprovalNodeData {
  label: string;
  assignmentType: 'user' | 'group';
  assignee: string;
  outcomes: ApprovalOutcome[];
  allowReassignment?: boolean;
  dueDateDays?: number;
  description?: string;
}

export interface SendEmailNodeData {
  label: string;
  to: string;
  subject: string;
  body: string;
  isHtml?: boolean;
  cc?: string;
}

export interface EndNodeData {
  label: string;
  status: 'completed' | 'terminated' | 'rejected';
  message: string;
}

export interface DecisionCondition {
  id: string;
  field: string;
  operator: 'equals' | 'not_equals' | 'greater_than' | 'less_than' | 'contains';
  value: string;
}

export interface DecisionNodeData {
  label: string;
  conditions: DecisionCondition[];
  trueOutcomeLabel: string;
  falseOutcomeLabel: string;
}

export interface StageSubWorkflowNode {
  id: string;
  type?: string;
  position: { x: number; y: number };
  data: WorkflowNodeCustomData;
  [key: string]: any;
}

export interface StageSubWorkflowEdge {
  id: string;
  source: string;
  target: string;
  sourceHandle?: string | null;
  targetHandle?: string | null;
  label?: any;
  animated?: boolean;
  style?: Record<string, any>;
  [key: string]: any;
}

export interface StageSubWorkflow {
  nodes: any[];
  edges: any[];
}

export interface StageNodeData {
  label: string;
  stageName: string;
  description?: string;
  stageColor?: string;
  subWorkflow: StageSubWorkflow;
}

export type NodePayloadData = 
  | TriggerNodeData 
  | ApprovalNodeData 
  | SendEmailNodeData 
  | EndNodeData 
  | DecisionNodeData
  | StageNodeData;

export interface WorkflowNodeCustomData extends Record<string, unknown> {
  nodeType: NodeType;
  label: string;
  triggerData?: TriggerNodeData;
  approvalData?: ApprovalNodeData;
  sendEmailData?: SendEmailNodeData;
  endData?: EndNodeData;
  decisionData?: DecisionNodeData;
  stageData?: StageNodeData;
  status?: 'idle' | 'running' | 'success' | 'failed' | 'skipped';
}

export interface WorkflowSchema {
  id: string;
  name: string;
  version: string;
  description: string;
  createdAt: string;
  updatedAt: string;
  nodes: Array<{
    id: string;
    type: string;
    position: { x: number; y: number };
    data: WorkflowNodeCustomData;
  }>;
  edges: Array<{
    id: string;
    source: string;
    target: string;
    sourceHandle?: string | null;
    targetHandle?: string | null;
    label?: string;
  }>;
}

export interface SavedWorkflowRecord {
  id: string;
  name: string;
  savedAt: string;
  schema: WorkflowSchema;
}

export interface ExecutionLog {
  timestamp: string;
  nodeId: string;
  nodeName: string;
  status: 'info' | 'success' | 'warning' | 'error';
  message: string;
  outcome?: string;
}

export type NavigationTab = 
  | 'dashboard' 
  | 'designer' 
  | 'instances' 
  | 'smartobjects' 
  | 'tasks' 
  | 'analytics' 
  | 'users' 
  | 'settings';

export type Environment = 'Development' | 'Staging' | 'Production';

export type InstanceStatus = 'RUNNING' | 'COMPLETED' | 'FAILED' | 'PENDING';

export interface WorkflowInstance {
  id: string;
  workflowName: string;
  templateKey: 'onboarding' | 'expense' | 'itAccess' | 'custom';
  requester: string;
  requesterAvatar: string;
  startedAt: string;
  currentStep: string;
  progress: number;
  status: InstanceStatus;
  duration: string;
  department: string;
}

export interface ActionItemTask {
  id: string;
  instanceId: string;
  title: string;
  workflowName: string;
  requester: string;
  requesterAvatar: string;
  department: string;
  requestedAt: string;
  dueDate: string;
  status: InstanceStatus;
  priority: 'High' | 'Medium' | 'Low';
  amount?: string;
  description: string;
  outcomes: ApprovalOutcome[];
}

export interface SmartObjectField {
  name: string;
  type: 'String' | 'Number' | 'Boolean' | 'DateTime' | 'GUID';
  isPrimaryKey?: boolean;
  isRequired?: boolean;
}

export type SmartObjectCategory = 'HR' | 'Finance' | 'IT' | 'Core' | 'Custom' | (string & {});

export interface SmartObjectEntity {
  id: string;
  name: string;
  displayName: string;
  description: string;
  category: SmartObjectCategory;
  recordsCount: number;
  fields: SmartObjectField[];
}

export type SmartObjectRecord = Record<string, any>;

export interface UserRecord {
  id: string;
  name: string;
  email: string;
  role: 'BPM Administrator' | 'Process Manager' | 'Workflow Developer' | 'End User';
  group: string;
  status: 'Active' | 'Inactive';
  avatarUrl: string;
}

export interface DepartmentVolume {
  department: string;
  percentage: number;
  instancesCount: number;
}

export interface AnalyticsKpiData {
  avgCompletionTime: string;
  slaComplianceRate: string;
  topBottleneckNode: string;
  topBottleneckQueueTime: string;
  totalInstancesProcessed: number;
  volumeByDepartment: DepartmentVolume[];
}

export interface SystemSettingsData {
  smtpHost: string;
  smtpPort: string;
  smtpSecurity: string;
  sqlConnectionString: string;
  sqlMaxPoolSize: number;
  sqlTimeoutSeconds: number;
  retentionDays: number;
  telemetryEnabled: boolean;
  mfaEnforced: boolean;
}

