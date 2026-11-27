import type {
  SavedWorkflowRecord,
  WorkflowInstance,
  ActionItemTask,
  SmartObjectEntity,
  SmartObjectRecord,
  UserRecord,
  UserAuditEntry,
  AnalyticsKpiData,
  SystemSettingsData,
  ViewFlowData,
  InstanceEventsPage,
  InstanceCommandResponse,
  ConnectionRecord,
  CreateConnectionPayload,
  ConnectionTestResult,
  ConnectionPermissionRecord,
  UpsertConnectionPermissionPayload,
  UserConnectionCredentialRecord,
  RotateConnectionSecretPayload,
  ConnectionUsageResponse,
  ConnectionEnvironmentConfigRecord,
  UpsertConnectionEnvironmentConfigPayload,
  DiscoveredEntity,
  GenerateSmartObjectFromConnectionPayload,
  SchemaDiffResult,
  TaskAssignmentRecord,
  DelegationRuleRecord,
  CreateDelegationRulePayload,
  PublicAuthProvidersResponse,
  AuthProviderRecord,
  UpsertAuthProviderPayload
} from '../types/workflow';
import type { UserDirectoryItem, SmartObjectLookupPage } from '../types/smartform';

export type AppMode = 'demo' | 'connected';

export interface AuthSession {
  token: string;
  user: UserRecord;
}

export interface ActiveBottleneck {
  instanceId: string;
  workflowKey: string;
  currentNodeId: string;
  currentNodeType: string;
  initiatorId: string;
  startedAt: string;
  pendingTaskId?: string;
  taskTitle?: string;
  assigneeId?: string;
  taskAssignedAt?: string;
}

/**
 * Port interface for workflow data access and remote operations.
 * Isolates UI and Zustand store from persistence mechanics.
 */
export interface WorkflowRepository {
  getMode(): AppMode;
  
  // Authentication (Connected Mode)
  login(email: string, password?: string): Promise<AuthSession>;
  logout(): void;
  getAuthSession(): AuthSession | null;
  changePassword?(currentPassword: string, newPassword: string): Promise<void>;

  // Workflow Definitions
  getDefinitions(): Promise<SavedWorkflowRecord[]>;
  saveDefinition(record: SavedWorkflowRecord): Promise<void>;
  deleteDefinition(id: string): Promise<void>;

  // Execution Instances
  getInstances(): Promise<WorkflowInstance[]>;
  startInstance(workflowKey: string, initiatorId: string, variables?: Record<string, unknown>): Promise<WorkflowInstance>;
  getBottlenecks(): Promise<ActiveBottleneck[]>;
  getInstanceFlow(id: string): Promise<ViewFlowData>;
  getInstanceEvents(id: string, afterSeq?: number, limit?: number): Promise<InstanceEventsPage>;
  suspendInstance(id: string, reason?: string): Promise<InstanceCommandResponse>;
  resumeInstance(id: string): Promise<InstanceCommandResponse>;
  cancelInstance(id: string, reason: string): Promise<InstanceCommandResponse>;
  retryInstance(id: string, mode?: 'retry_failed_node' | 'retry_from_node', fromNodeId?: string): Promise<InstanceCommandResponse>;

  // Tasks & Approvals
  getTasks(assigneeId?: string): Promise<ActionItemTask[]>;
  completeTask(
    taskId: string,
    outcome: string,
    comments?: string,
    formPatch?: Record<string, unknown>
  ): Promise<void>;
  delegateTask(
    taskId: string,
    targetAssigneeId: string,
    reason?: string,
    expectedVersion?: number
  ): Promise<void>;
  recallTask(taskId: string, reason?: string, expectedVersion?: number): Promise<void>;
  claimTask(taskId: string, expectedVersion?: number): Promise<void>;
  releaseTask(taskId: string, reason?: string, expectedVersion?: number): Promise<void>;
  getTaskAssignments(taskId: string): Promise<TaskAssignmentRecord[]>;

  // Out-of-Office Delegation Rules
  getDelegationRules(): Promise<DelegationRuleRecord[]>;
  createDelegationRule(payload: CreateDelegationRulePayload): Promise<DelegationRuleRecord>;
  deleteDelegationRule(id: string): Promise<void>;

  // Organizational & Data Models
  getUsers(): Promise<UserRecord[]>;
  searchUsers(query?: string, limit?: number): Promise<UserDirectoryItem[]>;
  createUser(user: Omit<UserRecord, 'id'>): Promise<UserRecord>;
  updateUser(id: string, updates: Pick<UserRecord, 'name' | 'role' | 'group' | 'status'> & { avatarUrl?: string }): Promise<UserRecord>;
  resetUserPassword(id: string, newPassword: string): Promise<void>;
  getUserAuditLog(limit?: number): Promise<UserAuditEntry[]>;
  deleteUser(id: string): Promise<void>;
  getSmartObjects(): Promise<SmartObjectEntity[]>;
  createSmartObject(entity: SmartObjectEntity): Promise<SmartObjectEntity>;
  updateSmartObject(id: string, updates: Partial<SmartObjectEntity>): Promise<SmartObjectEntity>;
  deleteSmartObject(id: string): Promise<void>;

  // SmartObject Data Items
  getSmartObjectRecords(smartObjectId: string): Promise<SmartObjectRecord[]>;
  lookupSmartObject(smartObjectId: string, query?: string, page?: number, pageSize?: number): Promise<SmartObjectLookupPage>;
  addSmartObjectRecord(smartObjectId: string, record: SmartObjectRecord): Promise<SmartObjectRecord>;
  updateSmartObjectRecord(smartObjectId: string, primaryKeyVal: string | number, record: SmartObjectRecord): Promise<SmartObjectRecord>;
  deleteSmartObjectRecord(smartObjectId: string, primaryKeyVal: string | number): Promise<void>;

  // Analytics & Settings
  getAnalyticsKpis(): Promise<AnalyticsKpiData>;
  getSettings(): Promise<SystemSettingsData>;
  saveSettings(settings: Partial<SystemSettingsData>): Promise<SystemSettingsData>;

  // External Connections (Service Brokers, Governance & Personal Vault)
  getConnections(): Promise<ConnectionRecord[]>;
  createConnection(payload: CreateConnectionPayload): Promise<ConnectionRecord>;
  deleteConnection(id: string): Promise<void>;
  testConnection(id: string): Promise<ConnectionTestResult>;
  rotateConnectionSecret(id: string, payload: RotateConnectionSecretPayload): Promise<ConnectionRecord>;
  getConnectionUsage(id: string, limit?: number): Promise<ConnectionUsageResponse>;
  getConnectionEnvironments(id: string): Promise<ConnectionEnvironmentConfigRecord[]>;
  upsertConnectionEnvironment(
    id: string,
    payload: UpsertConnectionEnvironmentConfigPayload
  ): Promise<ConnectionEnvironmentConfigRecord>;
  getConnectionPermissions(connectionId: string): Promise<ConnectionPermissionRecord[]>;
  upsertConnectionPermission(connectionId: string, payload: UpsertConnectionPermissionPayload): Promise<ConnectionPermissionRecord>;
  deleteConnectionPermission(connectionId: string, permissionId: string): Promise<void>;
  getMyConnectionCredentials(): Promise<UserConnectionCredentialRecord[]>;
  upsertMyConnectionCredential(connectionId: string, secretJson: string): Promise<UserConnectionCredentialRecord>;
  deleteMyConnectionCredential(connectionId: string): Promise<void>;
  discoverConnectionEntities(connectionId: string): Promise<DiscoveredEntity[]>;
  generateSmartObjectFromConnection(
    connectionId: string,
    payload: GenerateSmartObjectFromConnectionPayload
  ): Promise<SmartObjectEntity>;
  syncSmartObjectSchema(smartObjectId: string, apply?: boolean): Promise<SchemaDiffResult>;

  // External OIDC Authentication & Admin Providers
  getPublicAuthProviders(): Promise<PublicAuthProvidersResponse>;
  getExternalChallengeUrl(providerId: string): string;
  exchangeExternalAuthCode(exchangeCode: string): Promise<AuthSession>;
  getAdminAuthProviders(): Promise<AuthProviderRecord[]>;
  createAuthProvider(payload: UpsertAuthProviderPayload): Promise<AuthProviderRecord>;
  updateAuthProvider(id: string, payload: UpsertAuthProviderPayload): Promise<AuthProviderRecord>;
  deleteAuthProvider(id: string): Promise<void>;
}
