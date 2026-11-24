import type {
  SavedWorkflowRecord,
  WorkflowInstance,
  ActionItemTask,
  SmartObjectEntity,
  SmartObjectRecord,
  UserRecord,
  AnalyticsKpiData,
  SystemSettingsData
} from '../types/workflow';

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

  // Workflow Definitions
  getDefinitions(): Promise<SavedWorkflowRecord[]>;
  saveDefinition(record: SavedWorkflowRecord): Promise<void>;
  deleteDefinition(id: string): Promise<void>;

  // Execution Instances
  getInstances(): Promise<WorkflowInstance[]>;
  startInstance(workflowKey: string, initiatorId: string, variables?: Record<string, unknown>): Promise<WorkflowInstance>;
  getBottlenecks(): Promise<ActiveBottleneck[]>;

  // Tasks & Approvals
  getTasks(assigneeId?: string): Promise<ActionItemTask[]>;
  completeTask(taskId: string, outcome: string, completedBy: string, comments?: string): Promise<void>;

  // Organizational & Data Models
  getUsers(): Promise<UserRecord[]>;
  createUser(user: Omit<UserRecord, 'id'>): Promise<UserRecord>;
  deleteUser(id: string): Promise<void>;
  getSmartObjects(): Promise<SmartObjectEntity[]>;
  createSmartObject(entity: SmartObjectEntity): Promise<SmartObjectEntity>;
  updateSmartObject(id: string, updates: Partial<SmartObjectEntity>): Promise<SmartObjectEntity>;
  deleteSmartObject(id: string): Promise<void>;

  // SmartObject Data Items
  getSmartObjectRecords(smartObjectId: string): Promise<SmartObjectRecord[]>;
  addSmartObjectRecord(smartObjectId: string, record: SmartObjectRecord): Promise<SmartObjectRecord>;
  updateSmartObjectRecord(smartObjectId: string, primaryKeyVal: string | number, record: SmartObjectRecord): Promise<SmartObjectRecord>;
  deleteSmartObjectRecord(smartObjectId: string, primaryKeyVal: string | number): Promise<void>;

  // Analytics & Settings
  getAnalyticsKpis(): Promise<AnalyticsKpiData>;
  getSettings(): Promise<SystemSettingsData>;
  saveSettings(settings: Partial<SystemSettingsData>): Promise<SystemSettingsData>;
}
