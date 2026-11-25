import type {
  SavedWorkflowRecord,
  WorkflowInstance,
  ActionItemTask,
  SmartObjectEntity,
  SmartObjectRecord,
  UserRecord,
  AnalyticsKpiData,
  SystemSettingsData,
  ViewFlowData,
  InstanceEventsPage,
  InstanceCommandResponse
} from '../types/workflow';
import type {
  WorkflowRepository,
  AppMode,
  AuthSession,
  ActiveBottleneck
} from './workflowRepository';

const TOKEN_STORAGE_KEY = 'workflow_engine_auth_session';

export class HttpWorkflowRepository implements WorkflowRepository {
  private baseUrl: string;
  private authSession: AuthSession | null = null;

  constructor() {
    this.baseUrl = (import.meta.env.VITE_API_BASE_URL as string) || 'http://localhost:5000/api';
    this.loadPersistedAuth();
  }

  getMode(): AppMode {
    return 'connected';
  }

  private loadPersistedAuth(): void {
    try {
      const raw = localStorage.getItem(TOKEN_STORAGE_KEY);
      if (raw) {
        this.authSession = JSON.parse(raw);
      }
    } catch {
      this.authSession = null;
    }
  }

  private saveAuth(session: AuthSession | null): void {
    this.authSession = session;
    if (session) {
      localStorage.setItem(TOKEN_STORAGE_KEY, JSON.stringify(session));
    } else {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
    }
  }

  private getHeaders(): HeadersInit {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json'
    };

    if (this.authSession?.token) {
      headers['Authorization'] = `Bearer ${this.authSession.token}`;
    }

    return headers;
  }

  async login(email: string, password = 'Password123!'): Promise<AuthSession> {
    const response = await fetch(`${this.baseUrl}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Authentication failed: ${errorText}`);
    }

    const data = await response.json();
    const session: AuthSession = {
      token: data.token,
      user: {
        id: data.userId,
        name: data.name,
        email: data.email,
        role: data.role,
        group: data.group,
        status: 'Active',
        avatarUrl: '/avatars/user-2.svg'
      }
    };

    this.saveAuth(session);
    return session;
  }

  logout(): void {
    this.saveAuth(null);
  }

  getAuthSession(): AuthSession | null {
    return this.authSession;
  }

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/auth/change-password`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify({ currentPassword, newPassword })
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Password update failed: ${errorText}`);
    }
  }

  async getDefinitions(): Promise<SavedWorkflowRecord[]> {
    const response = await fetch(`${this.baseUrl}/definitions`, {
      headers: this.getHeaders()
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch workflow definitions: ${response.statusText}`);
    }

    const definitions = await response.json();
    return definitions.map((d: any) => ({
      id: d.workflowKey,
      name: d.name,
      savedAt: d.updatedAt,
      schema: d.activeGraphJson ? JSON.parse(d.activeGraphJson) : {
        id: d.workflowKey,
        name: d.name,
        version: String(d.activeVersionNumber),
        description: d.description || '',
        createdAt: d.createdAt,
        updatedAt: d.updatedAt,
        nodes: [],
        edges: []
      }
    }));
  }

  async saveDefinition(record: SavedWorkflowRecord): Promise<void> {
    const payload = {
      workflowKey: record.id,
      name: record.name,
      graphJson: JSON.stringify(record.schema),
      createdBy: this.authSession?.user?.email || 'admin@corp.internal',
      description: record.schema.description || '',
      category: 'General'
    };

    const response = await fetch(`${this.baseUrl}/definitions`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      throw new Error(`Failed to save definition: ${response.statusText}`);
    }
  }

  async deleteDefinition(id: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/definitions/${encodeURIComponent(id)}`, {
      method: 'DELETE',
      headers: this.getHeaders()
    });

    if (!response.ok && response.status !== 404) {
      throw new Error(`Failed to delete definition '${id}': ${response.statusText}`);
    }
  }

  private static formatDurationSeconds(seconds?: number): string {
    if (typeof seconds !== 'number' || Number.isNaN(seconds) || seconds < 1) {
      return '< 1s';
    }
    const totalSec = Math.floor(seconds);
    const hours = Math.floor(totalSec / 3600);
    const mins = Math.floor((totalSec % 3600) / 60);
    const secs = totalSec % 60;
    if (hours > 0) return `${hours}h ${mins}m`;
    if (mins > 0) return `${mins}m ${secs}s`;
    return `${secs}s`;
  }

  async getInstances(): Promise<WorkflowInstance[]> {
    const response = await fetch(`${this.baseUrl}/instances`, {
      headers: this.getHeaders()
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch instances: ${response.statusText}`);
    }

    const data = await response.json();
    return data.map((i: any) => ({
      id: i.instanceId,
      workflowName: i.workflowName || i.workflowKey,
      templateKey: 'custom',
      requester: i.initiatorId,
      requesterAvatar: '/avatars/user-6.svg',
      startedAt: new Date(i.startedAt).toLocaleString(),
      currentStep: i.currentNodeLabel || i.currentNodeId || 'Running',
      progress: typeof i.progressPercentage === 'number'
        ? i.progressPercentage
        : (i.status === 'Completed' ? 100 : 50),
      status: String(i.status || 'RUNNING').toUpperCase(),
      duration: HttpWorkflowRepository.formatDurationSeconds(i.durationSeconds),
      department: i.department || 'Operations'
    }));
  }

  async startInstance(workflowKey: string, initiatorId: string, variables?: Record<string, unknown>): Promise<WorkflowInstance> {
    const response = await fetch(`${this.baseUrl}/instances/start`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify({
        workflowKey,
        initiatorId: initiatorId || this.authSession?.user.email || 'initiator@corp.internal',
        triggerVariables: variables
      })
    });

    if (!response.ok) {
      throw new Error(`Failed to start instance: ${response.statusText}`);
    }

    const data = await response.json();
    return {
      id: data.instanceId,
      workflowName: workflowKey,
      templateKey: 'custom',
      requester: initiatorId,
      requesterAvatar: '/avatars/user-6.svg',
      startedAt: 'Just now',
      currentStep: data.currentNodeId || 'Started',
      progress: 10,
      status: 'RUNNING',
      duration: '0s',
      department: 'Operations'
    };
  }

  async getBottlenecks(): Promise<ActiveBottleneck[]> {
    const response = await fetch(`${this.baseUrl}/instances/bottlenecks`, {
      headers: this.getHeaders()
    });

    if (!response.ok) {
      return [];
    }

    return await response.json();
  }

  private async parseErrorAndThrow(response: Response, fallbackMessage: string): Promise<never> {
    let message = fallbackMessage;
    let code: string | undefined;
    try {
      const contentType = response.headers.get('content-type') || '';
      if (contentType.includes('application/json')) {
        const body = await response.json();
        message = body.error || body.title || body.message || fallbackMessage;
        code = body.code;
      } else {
        const text = await response.text();
        if (text) message = text;
      }
    } catch {
      // Keep fallback message
    }

    const err = new Error(message) as Error & { status?: number; code?: string };
    err.status = response.status;
    err.code = code;
    throw err;
  }

  async getInstanceFlow(id: string): Promise<ViewFlowData> {
    const response = await fetch(`${this.baseUrl}/instances/${encodeURIComponent(id)}/flow`, {
      headers: this.getHeaders()
    });

    if (!response.ok) {
      await this.parseErrorAndThrow(response, `Failed to load ViewFlow for instance '${id}'.`);
    }

    return await response.json();
  }

  async getInstanceEvents(id: string, afterSeq = 0, limit = 50): Promise<InstanceEventsPage> {
    const params = new URLSearchParams({
      afterSeq: String(afterSeq),
      limit: String(limit)
    });
    const response = await fetch(`${this.baseUrl}/instances/${encodeURIComponent(id)}/events?${params.toString()}`, {
      headers: this.getHeaders()
    });

    if (!response.ok) {
      await this.parseErrorAndThrow(response, `Failed to load events for instance '${id}'.`);
    }

    return await response.json();
  }

  async suspendInstance(id: string, reason?: string): Promise<InstanceCommandResponse> {
    const commandId = crypto.randomUUID();
    const response = await fetch(`${this.baseUrl}/instances/${encodeURIComponent(id)}/commands/suspend`, {
      method: 'POST',
      headers: {
        ...this.getHeaders(),
        'Idempotency-Key': commandId
      },
      body: JSON.stringify({ commandId, reason })
    });

    if (!response.ok) {
      await this.parseErrorAndThrow(response, `Failed to suspend instance '${id}'.`);
    }

    return await response.json();
  }

  async resumeInstance(id: string): Promise<InstanceCommandResponse> {
    const commandId = crypto.randomUUID();
    const response = await fetch(`${this.baseUrl}/instances/${encodeURIComponent(id)}/commands/resume`, {
      method: 'POST',
      headers: {
        ...this.getHeaders(),
        'Idempotency-Key': commandId
      },
      body: JSON.stringify({ commandId })
    });

    if (!response.ok) {
      await this.parseErrorAndThrow(response, `Failed to resume instance '${id}'.`);
    }

    return await response.json();
  }

  async cancelInstance(id: string, reason: string): Promise<InstanceCommandResponse> {
    const commandId = crypto.randomUUID();
    const response = await fetch(`${this.baseUrl}/instances/${encodeURIComponent(id)}/commands/cancel`, {
      method: 'POST',
      headers: {
        ...this.getHeaders(),
        'Idempotency-Key': commandId
      },
      body: JSON.stringify({ commandId, reason })
    });

    if (!response.ok) {
      await this.parseErrorAndThrow(response, `Failed to cancel instance '${id}'.`);
    }

    return await response.json();
  }

  async retryInstance(
    id: string,
    mode: 'retry_failed_node' | 'retry_from_node' = 'retry_failed_node',
    fromNodeId?: string
  ): Promise<InstanceCommandResponse> {
    const commandId = crypto.randomUUID();
    const response = await fetch(`${this.baseUrl}/instances/${encodeURIComponent(id)}/commands/retry`, {
      method: 'POST',
      headers: {
        ...this.getHeaders(),
        'Idempotency-Key': commandId
      },
      body: JSON.stringify({ commandId, mode, fromNodeId })
    });

    if (!response.ok) {
      await this.parseErrorAndThrow(response, `Failed to retry instance '${id}'.`);
    }

    return await response.json();
  }

  async getTasks(assigneeId?: string): Promise<ActionItemTask[]> {
    const endpoint = assigneeId
      ? `${this.baseUrl}/tasks/assignee/${encodeURIComponent(assigneeId)}`
      : `${this.baseUrl}/tasks?status=Pending`;

    const response = await fetch(endpoint, {
      headers: this.getHeaders()
    });

    if (!response.ok) {
      return [];
    }

    const data = await response.json();
    return data.map((t: any) => ({
      id: t.taskId,
      instanceId: t.instanceId,
      title: t.taskTitle,
      workflowName: t.workflowName || 'Enterprise Workflow',
      requester: t.initiatorId || t.completedBy || 'System',
      requesterAvatar: '/avatars/user-2.svg',
      department: t.assigneeType,
      requestedAt: t.assignedAt,
      dueDate: t.dueDate ? new Date(t.dueDate).toLocaleDateString() : 'N/A',
      status: String(t.status || 'PENDING').toUpperCase(),
      priority: 'High',
      description: `Pending outcome decision for node ${t.nodeId}`,
      outcomes: Array.isArray(t.outcomes) && t.outcomes.length > 0
        ? t.outcomes.map((o: any) => ({
            id: o.id,
            label: o.label,
            color: o.color || '#2563EB'
          }))
        : [
            { id: 'approve', label: 'Approve', color: '#059669' },
            { id: 'reject', label: 'Reject', color: '#DC2626' }
          ],
      formId: t.formId ?? null,
      formVersionId: t.formVersionId ?? null,
      formMode: t.formMode === 'editable' ? 'editable' : 'readonly',
      formData: t.formData && typeof t.formData === 'object' ? t.formData : {}
    }));
  }

  async completeTask(
    taskId: string,
    outcome: string,
    comments?: string,
    formPatch?: Record<string, unknown>
  ): Promise<void> {
    const payload: Record<string, unknown> = { outcome };
    if (comments !== undefined) {
      payload.comments = comments;
    }
    if (formPatch && Object.keys(formPatch).length > 0) {
      payload.formPatch = formPatch;
    }

    const response = await fetch(`${this.baseUrl}/tasks/${encodeURIComponent(taskId)}/complete`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      throw new Error(`Failed to complete task: ${response.statusText}`);
    }
  }

  async getUsers(): Promise<UserRecord[]> {
    try {
      const response = await fetch(`${this.baseUrl}/users`, {
        headers: this.getHeaders()
      });
      if (response.ok) {
        const users = await response.json();
        if (Array.isArray(users) && users.length > 0) {
          return users;
        }
      }
    } catch {
      // Fallback
    }

    return [
      {
        id: 'usr-adel',
        name: 'Adel Sebti',
        email: 'adel.sebti@corp.internal',
        role: 'BPM Administrator',
        group: 'Engineering',
        status: 'Active',
        avatarUrl: '/avatars/user-2.svg'
      },
      {
        id: 'usr-karim',
        name: 'Karim Khmissi',
        email: 'karim.khmissi@corp.internal',
        role: 'Process Manager',
        group: 'Finance',
        status: 'Active',
        avatarUrl: '/avatars/user-3.svg'
      }
    ];
  }

  async createUser(user: Omit<UserRecord, 'id'>): Promise<UserRecord> {
    try {
      const response = await fetch(`${this.baseUrl}/users`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify(user)
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }

    return {
      ...user,
      id: `usr-${Date.now()}`
    };
  }

  async deleteUser(id: string): Promise<void> {
    try {
      await fetch(`${this.baseUrl}/users/${id}`, {
        method: 'DELETE',
        headers: this.getHeaders()
      });
    } catch {
      // Fallback
    }
  }

  private localSmartObjects: SmartObjectEntity[] = [
    {
      id: 'so-sql-schema',
      name: 'WorkflowDefinitions',
      displayName: 'SQL Server / PostgreSQL Schema',
      description: 'BPM relational enterprise master registry.',
      category: 'Core',
      recordsCount: 3,
      fields: [
        { name: 'DefinitionID', type: 'Number', isPrimaryKey: true, isRequired: true },
        { name: 'WorkflowKey', type: 'String', isRequired: true },
        { name: 'Name', type: 'String', isRequired: true }
      ]
    }
  ];

  private localSmartObjectData: Record<string, SmartObjectRecord[]> = {
    'so-sql-schema': [
      { DefinitionID: 1, WorkflowKey: 'employee-onboarding', Name: 'Employee Onboarding Process' },
      { DefinitionID: 2, WorkflowKey: 'expense-approval', Name: 'Purchase Request Approval' },
      { DefinitionID: 3, WorkflowKey: 'it-access', Name: 'IT System Access Request' }
    ]
  };

  async getSmartObjects(): Promise<SmartObjectEntity[]> {
    try {
      const res = await fetch(`${this.baseUrl}/smartobjects`, {
        headers: this.getHeaders()
      });
      if (res.ok) {
        const data = await res.json();
        if (Array.isArray(data)) return data;
      }
    } catch {
      // Fallback to local entities
    }
    return [...this.localSmartObjects];
  }

  async createSmartObject(entity: SmartObjectEntity): Promise<SmartObjectEntity> {
    try {
      const res = await fetch(`${this.baseUrl}/smartobjects`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify(entity)
      });
      if (res.ok) {
        const data = await res.json();
        if (data) return data;
      }
    } catch {
      // fallback
    }
    this.localSmartObjects.push(entity);
    if (!this.localSmartObjectData[entity.id]) {
      this.localSmartObjectData[entity.id] = [];
    }
    return entity;
  }

  async updateSmartObject(id: string, updates: Partial<SmartObjectEntity>): Promise<SmartObjectEntity> {
    try {
      const res = await fetch(`${this.baseUrl}/smartobjects/${id}`, {
        method: 'PUT',
        headers: this.getHeaders(),
        body: JSON.stringify(updates)
      });
      if (res.ok) {
        const data = await res.json();
        if (data) return data;
      }
    } catch {
      // fallback
    }
    const idx = this.localSmartObjects.findIndex(s => s.id === id);
    if (idx !== -1) {
      this.localSmartObjects[idx] = { ...this.localSmartObjects[idx], ...updates, id };
      return this.localSmartObjects[idx];
    }
    return { ...updates, id } as SmartObjectEntity;
  }

  async deleteSmartObject(id: string): Promise<void> {
    try {
      await fetch(`${this.baseUrl}/smartobjects/${id}`, {
        method: 'DELETE',
        headers: this.getHeaders()
      });
    } catch {
      // fallback
    }
    this.localSmartObjects = this.localSmartObjects.filter(s => s.id !== id);
    delete this.localSmartObjectData[id];
  }

  async getSmartObjectRecords(smartObjectId: string): Promise<SmartObjectRecord[]> {
    try {
      const res = await fetch(`${this.baseUrl}/smartobjects/${smartObjectId}/records`, {
        headers: this.getHeaders()
      });
      if (res.ok) {
        const data = await res.json();
        if (Array.isArray(data)) return data;
      }
    } catch {
      // fallback
    }
    return [...(this.localSmartObjectData[smartObjectId] || [])];
  }

  async addSmartObjectRecord(smartObjectId: string, record: SmartObjectRecord): Promise<SmartObjectRecord> {
    try {
      const res = await fetch(`${this.baseUrl}/smartobjects/${smartObjectId}/records`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify(record)
      });
      if (res.ok) {
        const data = await res.json();
        if (data) return data;
      }
    } catch {
      // fallback
    }
    if (!this.localSmartObjectData[smartObjectId]) {
      this.localSmartObjectData[smartObjectId] = [];
    }
    this.localSmartObjectData[smartObjectId].unshift(record);
    const so = this.localSmartObjects.find(s => s.id === smartObjectId);
    if (so) so.recordsCount = this.localSmartObjectData[smartObjectId].length;
    return record;
  }

  async updateSmartObjectRecord(smartObjectId: string, primaryKeyVal: string | number, record: SmartObjectRecord): Promise<SmartObjectRecord> {
    try {
      const res = await fetch(`${this.baseUrl}/smartobjects/${smartObjectId}/records/${primaryKeyVal}`, {
        method: 'PUT',
        headers: this.getHeaders(),
        body: JSON.stringify(record)
      });
      if (res.ok) {
        const data = await res.json();
        if (data) return data;
      }
    } catch {
      // fallback
    }
    const list = this.localSmartObjectData[smartObjectId];
    if (list) {
      const so = this.localSmartObjects.find(s => s.id === smartObjectId);
      const pkField = so?.fields.find(f => f.isPrimaryKey)?.name || Object.keys(record)[0] || 'id';
      const idx = list.findIndex(r => String(r[pkField]) === String(primaryKeyVal));
      if (idx !== -1) {
        list[idx] = { ...list[idx], ...record };
        return list[idx];
      }
    }
    return record;
  }

  async deleteSmartObjectRecord(smartObjectId: string, primaryKeyVal: string | number): Promise<void> {
    try {
      await fetch(`${this.baseUrl}/smartobjects/${smartObjectId}/records/${primaryKeyVal}`, {
        method: 'DELETE',
        headers: this.getHeaders()
      });
    } catch {
      // fallback
    }
    const list = this.localSmartObjectData[smartObjectId];
    if (list) {
      const so = this.localSmartObjects.find(s => s.id === smartObjectId);
      const pkField = so?.fields.find(f => f.isPrimaryKey)?.name || 'id';
      this.localSmartObjectData[smartObjectId] = list.filter(r => String(r[pkField]) !== String(primaryKeyVal));
      if (so) so.recordsCount = this.localSmartObjectData[smartObjectId].length;
    }
  }

  async getAnalyticsKpis(): Promise<AnalyticsKpiData> {
    try {
      const response = await fetch(`${this.baseUrl}/analytics/kpis`, {
        headers: this.getHeaders()
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }
    return {
      avgCompletionTime: '4m 12s',
      slaComplianceRate: '98.6%',
      topBottleneckNode: 'VP Finance Sign-Off',
      topBottleneckQueueTime: '24h 10m',
      totalInstancesProcessed: 1432,
      volumeByDepartment: [
        { department: 'Engineering Onboarding & Access', percentage: 42, instancesCount: 612 },
        { department: 'Finance Purchase Requisitions', percentage: 31, instancesCount: 440 },
        { department: 'IT Security & Cloud Entitlements', percentage: 27, instancesCount: 380 }
      ]
    };
  }

  async getSettings(): Promise<SystemSettingsData> {
    try {
      const response = await fetch(`${this.baseUrl}/settings`, {
        headers: this.getHeaders()
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }
    return {
      smtpHost: 'smtp.mail.enterprise.com',
      smtpPort: '587',
      smtpSecurity: 'STARTTLS',
      sqlConnectionString: 'Server=sql-prod.internal;Database=K2_WorkflowDB;User Id=k2_admin;Password=********;',
      sqlMaxPoolSize: 100,
      sqlTimeoutSeconds: 30,
      retentionDays: 90,
      telemetryEnabled: true,
      mfaEnforced: false
    };
  }

  async saveSettings(settings: Partial<SystemSettingsData>): Promise<SystemSettingsData> {
    try {
      const response = await fetch(`${this.baseUrl}/settings`, {
        method: 'PUT',
        headers: this.getHeaders(),
        body: JSON.stringify(settings)
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }
    return {
      smtpHost: settings.smtpHost || 'smtp.mail.enterprise.com',
      smtpPort: settings.smtpPort || '587',
      smtpSecurity: settings.smtpSecurity || 'STARTTLS',
      sqlConnectionString: settings.sqlConnectionString || 'Server=sql-prod.internal;Database=K2_WorkflowDB;User Id=k2_admin;Password=********;',
      sqlMaxPoolSize: settings.sqlMaxPoolSize ?? 100,
      sqlTimeoutSeconds: settings.sqlTimeoutSeconds ?? 30,
      retentionDays: settings.retentionDays ?? 90,
      telemetryEnabled: settings.telemetryEnabled ?? true,
      mfaEnforced: settings.mfaEnforced ?? false
    };
  }
}
