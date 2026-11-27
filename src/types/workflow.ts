import type { Role } from './roles';
export type NodeType = 'trigger' | 'approval' | 'sendEmail' | 'end' | 'decision' | 'stage' | 'http' | 'smartObject' | 'parallelSplit' | 'parallelJoin';

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
  formId?: string;
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
  formId?: string;
  formMode?: 'readonly' | 'editable';
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

export type DecisionOperator =
  | 'equals'
  | 'not_equals'
  | 'greater_than'
  | 'greater_than_or_equal'
  | 'less_than'
  | 'less_than_or_equal'
  | 'contains'
  | 'not_contains'
  | 'starts_with'
  | 'ends_with'
  | 'is_empty'
  | 'is_not_empty';

export interface DecisionCondition {
  id: string;
  field: string;
  operator: DecisionOperator;
  value: string;
}

export interface DecisionNodeData {
  label: string;
  conditions: DecisionCondition[];
  logicalOperator?: 'AND' | 'OR';
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

export interface HttpRetryPolicy {
  maxAttempts: number;
  initialDelayMs: number;
  backoffMultiplier: number;
  maxDelayMs: number;
}

export interface HttpBusinessErrorConfig {
  routeTo?: string;
  errorVariable?: string;
}

export interface HttpNodeData {
  label?: string;
  connectionRef: string;
  method: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  path: string;
  headers?: Record<string, string>;
  queryParams?: Record<string, string>;
  body?: unknown;
  timeoutMs?: number;
  maxResponseBytes?: number;
  retryPolicy?: HttpRetryPolicy;
  responseMapping?: Record<string, string>;
  onBusinessError?: HttpBusinessErrorConfig;
  idempotencyKey?: string;
}

export type ConnectionAuthType = 'none' | 'basic' | 'bearer' | 'api_key' | 'oauth2_client_credentials';
export type ConnectionServiceType = 'rest' | 'postgres' | 'sqlserver' | 'sharepoint';
export type ConnectionAuthMode = 'static' | 'oauth2_client_credentials' | 'user_sso_vault' | 'on_behalf_of';
export type ConnectionSecretSource = 'internal_encrypted' | 'external_vault';
export type ConnectionHealthStatus = 'Unknown' | 'Healthy' | 'Degraded' | 'Unreachable';
export type ConnectionEnvironmentName = 'Dev' | 'Test' | 'Prod';

export interface ConnectionRecord {
  id: string;
  name: string;
  baseUrl: string;
  authType: ConnectionAuthType;
  secretRef?: string | null;
  hasSecretRef: boolean;
  allowedHosts: string[];
  defaultHeaders: Record<string, string>;
  version: number;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  serviceType?: ConnectionServiceType;
  authMode?: ConnectionAuthMode;
  secretSource?: ConnectionSecretSource;
  hasEncryptedCredentials?: boolean;
  externalSecretKey?: string | null;
  canUse?: boolean;
  canManage?: boolean;
  allowWrite?: boolean;
  maxRows?: number;
  secretExpiresAt?: string | null;
  lastRotatedAt?: string | null;
  healthStatus?: ConnectionHealthStatus;
  lastTestedAt?: string | null;
  lastUsedAt?: string | null;
  lastErrorSanitized?: string | null;
}

export interface CreateConnectionPayload {
  id?: string;
  name: string;
  baseUrl: string;
  authType: ConnectionAuthType;
  secretRef?: string | null;
  allowedHosts: string[];
  defaultHeaders?: Record<string, string>;
  serviceType?: ConnectionServiceType;
  authMode?: ConnectionAuthMode;
  secretSource?: ConnectionSecretSource;
  credentialsJson?: string | null;
  externalSecretKey?: string | null;
  allowWrite?: boolean;
  maxRows?: number;
  secretExpiresAt?: string | null;
}

export interface RotateConnectionSecretPayload {
  credentialsJson?: string | null;
  externalSecretKey?: string | null;
  secretExpiresAt?: string | null;
  expectedVersion?: number;
}

export interface ConnectionUsageLogRecord {
  id: string;
  connectionId: string;
  instanceId?: string | null;
  workflowKey?: string | null;
  nodeId?: string | null;
  actorId: string;
  success: boolean;
  durationMs: number;
  executedAt: string;
}

export interface ConnectionUsageResponse {
  connectionId: string;
  connectionName: string;
  healthStatus: ConnectionHealthStatus;
  lastTestedAt?: string | null;
  lastUsedAt?: string | null;
  lastRotatedAt?: string | null;
  secretExpiresAt?: string | null;
  lastErrorSanitized?: string | null;
  logs: ConnectionUsageLogRecord[];
}

export interface ConnectionEnvironmentConfigRecord {
  id: string;
  connectionId: string;
  environmentName: ConnectionEnvironmentName;
  baseUrlOverride?: string | null;
  hasEncryptedCredentialsOverride: boolean;
  externalSecretKeyOverride?: string | null;
  updatedAt: string;
}

export interface UpsertConnectionEnvironmentConfigPayload {
  environmentName: ConnectionEnvironmentName;
  baseUrlOverride?: string | null;
  credentialsJsonOverride?: string | null;
  clearCredentialsOverride?: boolean;
  externalSecretKeyOverride?: string | null;
}

export interface ConnectionTestResult {
  success: boolean;
  latencyMs: number;
  errorMessage?: string | null;
}

export interface ConnectionPermissionRecord {
  id: string;
  connectionId: string;
  subjectType: 'User' | 'Role';
  subjectId: string;
  canUse: boolean;
  canManage: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface UpsertConnectionPermissionPayload {
  subjectType: 'User' | 'Role';
  subjectId: string;
  canUse: boolean;
  canManage: boolean;
}

export interface UserConnectionCredentialRecord {
  connectionId: string;
  connectionName: string;
  serviceType: string;
  authMode: string;
  hasCredentials: boolean;
  updatedAt?: string | null;
}

export interface SmartObjectNodeData {
  label?: string;
  smartObjectRef: string;
  methodName: string;
  inputMapping?: Record<string, unknown>;
  outputMapping?: Record<string, string>;
  onBusinessError?: HttpBusinessErrorConfig;
  retryPolicy?: HttpRetryPolicy;
  idempotencyKey?: string;
}

export type ParallelSplitMode = 'all' | 'conditional' | 'dynamic_foreach';
export type ParallelFailurePolicy = 'fail_instance' | 'cancel_siblings' | 'continue_without';
export type ParallelJoinPolicy = 'all' | 'any' | 'n_of_m';
export type ParallelOnRemainingPolicy = 'cancel' | 'let_finish';
export type ParallelMergeStrategy = 'error_on_conflict' | 'last_writer_wins' | 'append' | 'sum' | 'first_non_null';

export interface ParallelSplitNodeData {
  label?: string;
  mode: ParallelSplitMode;
  collectionExpression?: string;
  itemVariable?: string;
  branchConditions?: Record<string, DecisionNodeData>;
  onBranchFailure?: ParallelFailurePolicy;
  joinNodeId?: string;
}

export interface ParallelJoinNodeData {
  label?: string;
  policy: ParallelJoinPolicy;
  quorumCount?: number;
  requiredCount?: number;
  onRemaining?: ParallelOnRemainingPolicy;
  mergeStrategy?: ParallelMergeStrategy;
  variableMergeStrategies?: Record<string, ParallelMergeStrategy>;
  splitNodeId?: string;
}

export type NodePayloadData =
  | TriggerNodeData
  | ApprovalNodeData
  | SendEmailNodeData
  | EndNodeData
  | DecisionNodeData
  | StageNodeData
  | HttpNodeData
  | SmartObjectNodeData
  | ParallelSplitNodeData
  | ParallelJoinNodeData;

export interface WorkflowNodeCustomData extends Record<string, unknown> {
  nodeType: NodeType;
  label: string;
  triggerData?: TriggerNodeData;
  approvalData?: ApprovalNodeData;
  sendEmailData?: SendEmailNodeData;
  endData?: EndNodeData;
  decisionData?: DecisionNodeData;
  stageData?: StageNodeData;
  httpData?: HttpNodeData;
  smartObjectData?: SmartObjectNodeData;
  splitData?: ParallelSplitNodeData;
  joinData?: ParallelJoinNodeData;
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
  | 'portal'
  | 'designer'
  | 'smartforms'
  | 'instances'
  | 'smartobjects'
  | 'tasks'
  | 'analytics'
  | 'users'
  | 'settings';

export type Environment = 'Development' | 'Staging' | 'Production';

export type InstanceStatus =
  | 'CREATED'
  | 'RUNNING'
  | 'WAITING'
  | 'SUSPENDED'
  | 'FAULTED'
  | 'CANCELLED'
  | 'COMPLETED'
  | 'FAILED'
  | 'PENDING';

export interface ViewFlowInstanceInfo {
  id: string;
  status: string;
  suspendedFromStatus?: string | null;
  startedAt: string;
  endedAt?: string | null;
  version: number;
  definitionId: number;
  definitionVersion: number;
}

export interface ViewFlowNodeExecution {
  nodeId: string;
  type: string;
  status: string;
  attempts: number;
  startedAt: string;
  endedAt?: string | null;
  error?: string | null;
}

export interface InstanceEventRecord {
  id: number;
  instanceId: string;
  seq: number;
  type: string;
  nodeId?: string | null;
  actorId?: string | null;
  actorType: string;
  payloadJson?: string | null;
  createdAt: string;
}

export interface ViewFlowToken {
  id: string;
  parentTokenId?: string | null;
  nodeId: string;
  status: 'Active' | 'Waiting' | 'Completed' | 'Cancelled' | 'Faulted' | string;
  branchId?: string | null;
  splitNodeId?: string | null;
  version: number;
}

export interface ViewFlowJoinState {
  id: string;
  splitNodeId: string;
  joinNodeId: string;
  expectedCount: number;
  arrivedCount: number;
  joinFired: boolean;
  version: number;
}

export interface ViewFlowData {
  instance: ViewFlowInstanceInfo;
  definitionJson: string;
  nodes: ViewFlowNodeExecution[];
  activeNodes: string[];
  activeTokens: string[];
  timeline: InstanceEventRecord[];
  variables: Record<string, unknown>;
  tokens?: ViewFlowToken[];
  joinStates?: ViewFlowJoinState[];
}

export interface InstanceEventsPage {
  instanceId: string;
  afterSeq: number;
  limit: number;
  nextSeq?: number | null;
  hasMore: boolean;
  events: InstanceEventRecord[];
}

export interface InstanceCommandResponse {
  instanceId: string;
  status: string;
  command: string;
  commandId: string;
  executedAt: string;
  message: string;
}

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

export interface TaskAssignmentRecord {
  id: number;
  taskId: string;
  action: string;
  fromAssigneeId?: string | null;
  toAssigneeId: string;
  toAssigneeType: string;
  actorUserId: string;
  reason?: string | null;
  createdAt: string;
}

export interface DelegationRuleRecord {
  id: string;
  delegatorId: string;
  delegateId: string;
  scope: string;
  validFrom: string;
  validTo: string;
  active: boolean;
  reason?: string | null;
  createdBy: string;
  createdAt: string;
}

export interface CreateDelegationRulePayload {
  delegateId: string;
  validFrom: string;
  validTo: string;
  scope?: string;
  reason?: string;
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
  formId?: string | null;
  formVersionId?: string | null;
  formMode: 'readonly' | 'editable';
  formData: Record<string, unknown>;
  assigneeType?: string;
  assigneeId?: string;
  originalAssigneeId?: string | null;
  resolutionPolicy?: 'Single' | 'Claim' | 'Consensus' | 'Quorum' | string;
  quorumCount?: number | null;
  claimedByUserId?: string | null;
  claimedAt?: string | null;
  dueAt?: string | null;
  completedByUserId?: string | null;
  onBehalfOfUserId?: string | null;
  escalatedAt?: string | null;
  version?: number;
  metadataJson?: string | null;
}

export interface SmartObjectField {
  name: string;
  type: 'String' | 'Number' | 'Boolean' | 'DateTime' | 'GUID' | 'string' | 'number' | 'boolean' | 'datetime' | 'json' | (string & {});
  displayName?: string;
  isPrimaryKey?: boolean;
  isRequired?: boolean;
  masked?: boolean;
}

export type SmartObjectCategory = 'HR' | 'Finance' | 'IT' | 'Core' | 'Custom' | (string & {});

export type SmartObjectStatus = 'Draft' | 'Published' | 'Archived';

export type SmartObjectMethodKind = 'read' | 'create' | 'update' | 'delete' | 'custom';

export interface SmartObjectMethodParameter {
  name: string;
  type: string;
  required?: boolean;
  isSensitive?: boolean;
}

export interface SmartObjectMethodBinding {
  provider: 'InternalStore' | 'HttpBinding' | (string & {});
  connectionRef?: string;
  httpMethod?: string;
  path?: string;
  headers?: Record<string, string>;
  queryParams?: Record<string, string>;
  responseMapping?: Record<string, string>;
  recordKeyInput?: string;
  timeoutMs?: number;
}

export interface SmartObjectMethodContract {
  name: string;
  kind: SmartObjectMethodKind;
  inputs?: SmartObjectMethodParameter[];
  outputs?: SmartObjectMethodParameter[];
  idempotent: boolean;
  binding?: SmartObjectMethodBinding;
}

export interface SmartObjectEntity {
  id: string;
  name: string;
  displayName: string;
  description: string;
  category: SmartObjectCategory;
  recordsCount: number;
  fields: SmartObjectField[];
  version?: number;
  status?: SmartObjectStatus;
  objectRef?: string;
  methods?: SmartObjectMethodContract[];
  publishedAt?: string | null;
  publishedBy?: string | null;
  sourceConnectionId?: string | null;
  sourceEntityName?: string | null;
  primaryKeyField?: string | null;
}

export interface DiscoveredField {
  name: string;
  sourceType: string;
  mappedSmartObjectType: 'string' | 'number' | 'boolean' | 'datetime' | 'json' | string;
  isNullable: boolean;
  isPrimaryKey: boolean;
  suggestedMasked: boolean;
}

export interface DiscoveredEntity {
  entityName: string;
  displayName: string;
  detectedPrimaryKey?: string | null;
  fields: DiscoveredField[];
  schemaName?: string | null;
  entityType?: string | null;
}

export interface SelectedSmartObjectFieldPayload {
  name: string;
  displayName?: string;
  type: string;
  masked: boolean;
  isRequired?: boolean;
}

export interface GenerateSmartObjectFromConnectionPayload {
  entityName: string;
  smartObjectKey: string;
  displayName?: string;
  primaryKeyField?: string | null;
  selectedFields: SelectedSmartObjectFieldPayload[];
  description?: string;
  category?: string;
  publishImmediately?: boolean;
}

export interface SchemaFieldTypeChange {
  name: string;
  previousType: string;
  newType: string;
  sourceType: string;
}

export interface SchemaDiffResult {
  smartObjectId: string;
  connectionId: string;
  entityName: string;
  addedFields: DiscoveredField[];
  removedFields: SmartObjectField[];
  typeChangedFields: SchemaFieldTypeChange[];
  primaryKeyChanged: boolean;
  previousPrimaryKey?: string | null;
  newPrimaryKey?: string | null;
  hasChanges: boolean;
  applied: boolean;
}

export type SmartObjectRecord = Record<string, any>;

export interface UserAuditEntry {
  id: string;
  actorId: string;
  actorEmail: string;
  action: string;
  targetUserId: string;
  targetEmail: string;
  details?: string | null;
  occurredAt: string;
}

export interface UserRecord {
  id: string;
  name: string;
  email: string;
  role: Role;
  group: string;
  status: 'Active' | 'Inactive';
  avatarUrl: string;
  /** Write-only: sent when creating a user, never returned by the API. */
  password?: string;
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

export interface PublicAuthProvider {
  id: string;
  name: string;
  protocol: string;
}

export interface PublicAuthProvidersResponse {
  localLoginEnabled: boolean;
  providers: PublicAuthProvider[];
}

export interface AuthProviderRecord {
  id: string;
  name: string;
  protocol: string;
  authority: string;
  clientId: string;
  hasSecret: boolean;
  scopes: string;
  roleMappingJson: string;
  jitProvisioningEnabled: boolean;
  allowEmailMatching: boolean;
  allowAdminRoleMapping: boolean;
  defaultRole: string;
  isEnabled: boolean;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface UpsertAuthProviderPayload {
  name: string;
  protocol?: string;
  authority: string;
  clientId: string;
  clientSecret?: string;
  clearClientSecret?: boolean;
  scopes?: string;
  roleMappingJson?: string;
  jitProvisioningEnabled?: boolean;
  allowEmailMatching?: boolean;
  allowAdminRoleMapping?: boolean;
  defaultRole?: string;
  isEnabled?: boolean;
  expectedVersion?: number;
}

