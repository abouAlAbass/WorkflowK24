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
  InstanceCommandResponse,
  InstanceEventRecord,
  ViewFlowNodeExecution
} from '../types/workflow';
import type {
  WorkflowRepository,
  AppMode,
  AuthSession,
  ActiveBottleneck
} from './workflowRepository';

const DEMO_STORAGE_KEY = 'workflow_engine_demo_records';

const INITIAL_DEMO_INSTANCES: WorkflowInstance[] = [
  {
    id: 'INST-9021',
    workflowName: 'Employee Onboarding Process',
    templateKey: 'onboarding',
    requester: 'Adel Sebti',
    requesterAvatar: '/avatars/user-2.svg',
    startedAt: '10 mins ago',
    currentStep: 'Department Head Approval',
    progress: 50,
    status: 'RUNNING',
    duration: '10m 14s',
    department: 'Engineering'
  },
  {
    id: 'INST-9020',
    workflowName: 'Executive Purchase Request ($4,500)',
    templateKey: 'expense',
    requester: 'Karim Khmissi',
    requesterAvatar: '/avatars/user-3.svg',
    startedAt: '45 mins ago',
    currentStep: 'VP Finance Sign-Off',
    progress: 75,
    status: 'PENDING',
    duration: '45m 02s',
    department: 'Finance'
  },
  {
    id: 'INST-9019',
    workflowName: 'VPN & Server Admin Access',
    templateKey: 'itAccess',
    requester: 'Yacine Benali',
    requesterAvatar: '/avatars/user-1.svg',
    startedAt: '2 hours ago',
    currentStep: 'AD Provisioning Script',
    progress: 100,
    status: 'COMPLETED',
    duration: '1h 42m',
    department: 'IT Security'
  },
  {
    id: 'INST-9018',
    workflowName: 'Emergency System Maintenance',
    templateKey: 'custom',
    requester: 'DevOps Automated Webhook',
    requesterAvatar: '/avatars/user-4.svg',
    startedAt: '3 hours ago',
    currentStep: 'Database Backup Verification',
    progress: 30,
    status: 'FAILED',
    duration: '12m 45s',
    department: 'Infrastructure'
  }
];

const INITIAL_DEMO_TASKS: ActionItemTask[] = [
  {
    id: 'TASK-101',
    instanceId: 'INST-9021',
    title: 'Approve Senior Developer Hire (Sofiane Haddad)',
    workflowName: 'Employee Onboarding Process',
    requester: 'Adel Sebti',
    requesterAvatar: '/avatars/user-2.svg',
    department: 'Engineering',
    requestedAt: '2026-08-08T14:30:00Z',
    dueDate: 'Today at 5:00 PM',
    status: 'PENDING',
    priority: 'High',
    amount: '$120,000 / yr',
    description: 'New hire requisition for Senior Full Stack Developer position in Core Platform Team.',
    outcomes: [
      { id: 'approve', label: 'Approve Hire', color: '#059669' },
      { id: 'reject', label: 'Reject', color: '#DC2626' },
      { id: 'info', label: 'Request Revision', color: '#D97706' }
    ],
    formId: 'form-onboarding',
    formMode: 'readonly',
    formData: {
      FullName: 'Sofiane Haddad',
      Department: 'Engineering',
      Salary: '120000',
      HireDate: '2026-09-01',
      RequiresVpnHardware: true
    }
  },
  {
    id: 'TASK-102',
    instanceId: 'INST-9020',
    title: 'Hardware Order: 5x MacBook Pro M3 Max',
    workflowName: 'Executive Purchase Request ($4,500)',
    requester: 'Karim Khmissi',
    requesterAvatar: '/avatars/user-3.svg',
    department: 'Finance',
    requestedAt: '2026-08-08T11:15:00Z',
    dueDate: 'Tomorrow at 12:00 PM',
    status: 'PENDING',
    priority: 'Medium',
    amount: '$14,500 total',
    description: 'Hardware refresh for core engineering team leads.',
    outcomes: [
      { id: 'approve', label: 'Authorize PO', color: '#059669' },
      { id: 'reject', label: 'Decline PO', color: '#DC2626' }
    ],
    formId: 'form-purchase-req',
    formMode: 'editable',
    formData: {
      VendorName: 'Dell Technologies',
      Department: 'Engineering',
      Amount: '14500',
      NeededByDate: '2026-09-15',
      ExecutiveJustification: 'Hardware refresh for core engineering team leads.'
    }
  }
];

const INITIAL_DEMO_USERS: UserRecord[] = [
  {
    id: 'usr-1',
    name: 'Adel Sebti',
    email: 'adel.sebti@corp.internal',
    role: 'BPM Administrator',
    group: 'Engineering',
    status: 'Active',
    avatarUrl: '/avatars/user-2.svg'
  },
  {
    id: 'usr-2',
    name: 'Karim Khmissi',
    email: 'karim.khmissi@corp.internal',
    role: 'Process Manager',
    group: 'Finance',
    status: 'Active',
    avatarUrl: '/avatars/user-3.svg'
  }
];

export class InMemoryWorkflowRepository implements WorkflowRepository {
  private instances: WorkflowInstance[] = [...INITIAL_DEMO_INSTANCES];
  private tasks: ActionItemTask[] = [...INITIAL_DEMO_TASKS];
  private users: UserRecord[] = [...INITIAL_DEMO_USERS];
  private authSession: AuthSession | null = {
    token: 'demo-local-jwt-token',
    user: INITIAL_DEMO_USERS[0]
  };

  private smartObjects: SmartObjectEntity[] = [
    {
      id: 'so-employee',
      name: 'EmployeeRecord',
      displayName: 'Employee SmartObject',
      description: 'Core HR database entity for employees, departments, and titles.',
      category: 'HR',
      recordsCount: 5,
      fields: [
        { name: 'EmpID', type: 'GUID', isPrimaryKey: true, isRequired: true },
        { name: 'FullName', type: 'String', isRequired: true },
        { name: 'Department', type: 'String', isRequired: true },
        { name: 'Salary', type: 'Number' },
        { name: 'HireDate', type: 'DateTime' }
      ]
    },
    {
      id: 'so-expense',
      name: 'PurchaseRequest',
      displayName: 'Purchase Request SmartObject',
      description: 'Finance entity storing PO requisitions, vendors, and line items.',
      category: 'Finance',
      recordsCount: 3,
      fields: [
        { name: 'RequestID', type: 'GUID', isPrimaryKey: true, isRequired: true },
        { name: 'VendorName', type: 'String', isRequired: true },
        { name: 'TotalAmount', type: 'Number', isRequired: true },
        { name: 'CostCenter', type: 'String' },
        { name: 'CreatedDate', type: 'DateTime' }
      ]
    },
    {
      id: 'so-itaccess',
      name: 'ITAccessTicket',
      displayName: 'IT Access SmartObject',
      description: 'Security ticketing entity for Active Directory & cloud permissions.',
      category: 'IT',
      recordsCount: 3,
      fields: [
        { name: 'TicketID', type: 'GUID', isPrimaryKey: true, isRequired: true },
        { name: 'TargetSystem', type: 'String', isRequired: true },
        { name: 'AccessLevel', type: 'String', isRequired: true },
        { name: 'IsTemporary', type: 'Boolean' }
      ]
    }
  ];

  private smartObjectData: Record<string, SmartObjectRecord[]> = {
    'so-employee': [
      { EmpID: 'EMP-001', FullName: 'Adel Sebti', Department: 'Engineering', Salary: 115000, HireDate: '2022-03-15' },
      { EmpID: 'EMP-002', FullName: 'Karim Khmissi', Department: 'Finance', Salary: 98000, HireDate: '2021-08-01' },
      { EmpID: 'EMP-003', FullName: 'Mohamed Fetni', Department: 'Operations', Salary: 105000, HireDate: '2020-01-10' },
      { EmpID: 'EMP-004', FullName: 'David Kim', Department: 'IT', Salary: 112000, HireDate: '2023-05-20' },
      { EmpID: 'EMP-005', FullName: 'Yacine Benali', Department: 'HR', Salary: 92000, HireDate: '2022-11-03' }
    ],
    'so-expense': [
      { RequestID: 'REQ-101', VendorName: 'Dell Technologies', TotalAmount: 4500, CostCenter: 'CC-ENG-01', CreatedDate: '2026-10-10' },
      { RequestID: 'REQ-102', VendorName: 'Amazon Web Services', TotalAmount: 12850, CostCenter: 'CC-OPS-03', CreatedDate: '2026-10-12' },
      { RequestID: 'REQ-103', VendorName: 'Office Depot Logistics', TotalAmount: 750, CostCenter: 'CC-HR-02', CreatedDate: '2026-10-15' }
    ],
    'so-itaccess': [
      { TicketID: 'TKT-881', TargetSystem: 'Active Directory Admin', AccessLevel: 'ReadWrite', IsTemporary: false },
      { TicketID: 'TKT-882', TargetSystem: 'AWS Cloud Console', AccessLevel: 'ReadOnly', IsTemporary: true },
      { TicketID: 'TKT-883', TargetSystem: 'Production SQL Database', AccessLevel: 'Admin', IsTemporary: true }
    ]
  };

  getMode(): AppMode {
    return 'demo';
  }

  async login(email: string): Promise<AuthSession> {
    const user = INITIAL_DEMO_USERS.find(u => u.email.toLowerCase() === email.toLowerCase()) || {
      id: `usr-${Date.now()}`,
      name: email.split('@')[0],
      email: email,
      role: 'End User',
      group: 'General',
      status: 'Active',
      avatarUrl: '/avatars/user-6.svg'
    };

    this.authSession = {
      token: `demo-token-${Date.now()}`,
      user
    };

    return this.authSession;
  }

  logout(): void {
    this.authSession = null;
  }

  getAuthSession(): AuthSession | null {
    return this.authSession;
  }

  async changePassword(_currentPassword: string, newPassword: string): Promise<void> {
    if (!newPassword || newPassword.length < 8) {
      throw new Error('New password must contain at least 8 characters.');
    }
    return Promise.resolve();
  }

  async getDefinitions(): Promise<SavedWorkflowRecord[]> {
    try {
      const raw = localStorage.getItem(DEMO_STORAGE_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }

  async saveDefinition(record: SavedWorkflowRecord): Promise<void> {
    const current = await this.getDefinitions();
    const index = current.findIndex(w => w.id === record.id);
    const updated = index >= 0 ? current.map((w, i) => i === index ? record : w) : [record, ...current];
    localStorage.setItem(DEMO_STORAGE_KEY, JSON.stringify(updated));
  }

  async deleteDefinition(id: string): Promise<void> {
    const current = await this.getDefinitions();
    const updated = current.filter(w => w.id !== id);
    localStorage.setItem(DEMO_STORAGE_KEY, JSON.stringify(updated));
  }

  async getInstances(): Promise<WorkflowInstance[]> {
    return [...this.instances];
  }

  async startInstance(workflowKey: string, initiatorId: string): Promise<WorkflowInstance> {
    const newInst: WorkflowInstance = {
      id: `INST-${Math.floor(1000 + Math.random() * 9000)}`,
      workflowName: workflowKey,
      templateKey: 'custom',
      requester: initiatorId,
      requesterAvatar: '/avatars/user-6.svg',
      startedAt: 'Just now',
      currentStep: 'Initial Step',
      progress: 10,
      status: 'RUNNING',
      duration: '0s',
      department: 'Operations'
    };

    this.instances = [newInst, ...this.instances];
    return newInst;
  }

  async getBottlenecks(): Promise<ActiveBottleneck[]> {
    return this.instances
      .filter(i => i.status === 'RUNNING' || i.status === 'PENDING')
      .map(i => ({
        instanceId: i.id,
        workflowKey: i.templateKey,
        currentNodeId: 'node-appr',
        currentNodeType: 'approval',
        initiatorId: i.requester,
        startedAt: i.startedAt
      }));
  }

  private instanceEventsStore: Record<string, InstanceEventRecord[]> = {};
  private instanceSuspendedFrom: Record<string, WorkflowInstance['status']> = {};
  private instanceVersions: Record<string, number> = {};

  private ensureDemoEvents(inst: WorkflowInstance): InstanceEventRecord[] {
    if (!this.instanceEventsStore[inst.id]) {
      const baseTime = new Date(Date.now() - 10 * 60 * 1000).toISOString();
      this.instanceEventsStore[inst.id] = [
        {
          id: 1,
          instanceId: inst.id,
          seq: 1,
          type: 'Instance.Started',
          nodeId: 'trig-1',
          actorId: inst.requester,
          actorType: 'user',
          payloadJson: JSON.stringify({ workflowName: inst.workflowName }),
          createdAt: baseTime
        }
      ];
    }
    return this.instanceEventsStore[inst.id];
  }

  private appendDemoEvent(instanceId: string, type: string, nodeId: string | null, payload: Record<string, unknown>): void {
    const list = this.instanceEventsStore[instanceId] || [];
    const nextSeq = list.length > 0 ? list[list.length - 1].seq + 1 : 1;
    list.push({
      id: Date.now() + nextSeq,
      instanceId,
      seq: nextSeq,
      type,
      nodeId,
      actorId: this.authSession?.user?.email || 'demo@corp.internal',
      actorType: 'user',
      payloadJson: JSON.stringify(payload),
      createdAt: new Date().toISOString()
    });
    this.instanceEventsStore[instanceId] = list;
  }

  async getInstanceFlow(id: string): Promise<ViewFlowData> {
    const inst = this.instances.find(i => i.id === id);
    if (!inst) {
      const err = new Error(`Workflow instance '${id}' not found.`) as Error & { status?: number };
      err.status = 404;
      throw err;
    }

    const timeline = this.ensureDemoEvents(inst);
    const nowIso = new Date().toISOString();
    const startIso = new Date(Date.now() - 10 * 60 * 1000).toISOString();
    const normalizedStatus = inst.status === 'FAILED' ? 'Faulted'
      : inst.status === 'PENDING' ? 'Waiting'
      : inst.status.charAt(0) + inst.status.slice(1).toLowerCase();

    const nodes: ViewFlowNodeExecution[] = [
      {
        nodeId: 'trig-1',
        type: 'trigger',
        status: 'Completed',
        attempts: 1,
        startedAt: startIso,
        endedAt: startIso
      },
      {
        nodeId: 'node-current',
        type: 'approval',
        status: normalizedStatus === 'Completed' ? 'Completed'
          : normalizedStatus === 'Faulted' ? 'Failed'
          : normalizedStatus === 'Cancelled' ? 'Cancelled'
          : 'Waiting',
        attempts: 1,
        startedAt: startIso,
        endedAt: normalizedStatus === 'Completed' || normalizedStatus === 'Faulted' || normalizedStatus === 'Cancelled' ? nowIso : null,
        error: normalizedStatus === 'Faulted' ? 'Simulated execution failure on node' : null
      }
    ];

    const isTerminal = normalizedStatus === 'Completed' || normalizedStatus === 'Cancelled';
    const activeNodes = isTerminal ? [] : ['node-current'];

    return {
      instance: {
        id: inst.id,
        status: normalizedStatus,
        suspendedFromStatus: this.instanceSuspendedFrom[inst.id] ?? null,
        startedAt: startIso,
        endedAt: isTerminal ? nowIso : null,
        version: this.instanceVersions[inst.id] ?? 1,
        definitionId: 1,
        definitionVersion: 1
      },
      definitionJson: JSON.stringify({ id: inst.templateKey, name: inst.workflowName, version: '1.0', nodes: [], edges: [] }),
      nodes,
      activeNodes,
      activeTokens: activeNodes,
      timeline: [...timeline],
      variables: {
        department: inst.department,
        requester: inst.requester
      }
    };
  }

  async getInstanceEvents(id: string, afterSeq = 0, limit = 50): Promise<InstanceEventsPage> {
    const inst = this.instances.find(i => i.id === id);
    if (!inst) {
      const err = new Error(`Workflow instance '${id}' not found.`) as Error & { status?: number };
      err.status = 404;
      throw err;
    }

    const boundedLimit = Math.max(1, Math.min(200, limit));
    const all = this.ensureDemoEvents(inst).filter(e => e.seq > afterSeq);
    const slice = all.slice(0, boundedLimit);
    return {
      instanceId: id,
      afterSeq,
      limit: boundedLimit,
      nextSeq: slice.length > 0 ? slice[slice.length - 1].seq : null,
      hasMore: all.length > boundedLimit,
      events: slice
    };
  }

  async suspendInstance(id: string, reason?: string): Promise<InstanceCommandResponse> {
    const commandId = crypto.randomUUID();
    const inst = this.instances.find(i => i.id === id);
    if (!inst) {
      const err = new Error(`Workflow instance '${id}' not found.`) as Error & { status?: number };
      err.status = 404;
      throw err;
    }
    if (inst.status !== 'RUNNING' && inst.status !== 'WAITING' && inst.status !== 'PENDING') {
      const err = new Error(`Cannot suspend instance in status '${inst.status}'.`) as Error & { status?: number; code?: string };
      err.status = 409;
      err.code = 'INVALID_TRANSITION';
      throw err;
    }

    this.instanceSuspendedFrom[id] = inst.status;
    inst.status = 'SUSPENDED';
    this.instanceVersions[id] = (this.instanceVersions[id] ?? 1) + 1;
    this.ensureDemoEvents(inst);
    this.appendDemoEvent(id, 'Instance.Suspended', 'node-current', { reason: reason || 'Suspended by user' });

    return {
      instanceId: id,
      status: 'Suspended',
      command: 'Suspend',
      commandId,
      executedAt: new Date().toISOString(),
      message: `Instance '${id}' suspended successfully.`
    };
  }

  async resumeInstance(id: string): Promise<InstanceCommandResponse> {
    const commandId = crypto.randomUUID();
    const inst = this.instances.find(i => i.id === id);
    if (!inst) {
      const err = new Error(`Workflow instance '${id}' not found.`) as Error & { status?: number };
      err.status = 404;
      throw err;
    }
    if (inst.status !== 'SUSPENDED') {
      const err = new Error(`Cannot resume instance in status '${inst.status}'.`) as Error & { status?: number; code?: string };
      err.status = 409;
      err.code = 'INVALID_TRANSITION';
      throw err;
    }

    inst.status = this.instanceSuspendedFrom[id] || 'RUNNING';
    delete this.instanceSuspendedFrom[id];
    this.instanceVersions[id] = (this.instanceVersions[id] ?? 1) + 1;
    this.ensureDemoEvents(inst);
    this.appendDemoEvent(id, 'Instance.Resumed', 'node-current', {});

    return {
      instanceId: id,
      status: inst.status === 'PENDING' ? 'Waiting' : 'Running',
      command: 'Resume',
      commandId,
      executedAt: new Date().toISOString(),
      message: `Instance '${id}' resumed successfully.`
    };
  }

  async cancelInstance(id: string, reason: string): Promise<InstanceCommandResponse> {
    const commandId = crypto.randomUUID();
    if (!reason || reason.trim().length < 10) {
      const err = new Error('Cancellation reason must be at least 10 characters long.') as Error & { status?: number };
      err.status = 400;
      throw err;
    }
    const inst = this.instances.find(i => i.id === id);
    if (!inst) {
      const err = new Error(`Workflow instance '${id}' not found.`) as Error & { status?: number };
      err.status = 404;
      throw err;
    }
    if (inst.status === 'COMPLETED' || inst.status === 'CANCELLED') {
      const err = new Error(`Cannot cancel instance in terminal status '${inst.status}'.`) as Error & { status?: number; code?: string };
      err.status = 409;
      err.code = 'INVALID_TRANSITION';
      throw err;
    }

    inst.status = 'CANCELLED';
    this.instanceVersions[id] = (this.instanceVersions[id] ?? 1) + 1;
    this.ensureDemoEvents(inst);
    this.appendDemoEvent(id, 'Instance.Cancelled', 'node-current', { reason });

    return {
      instanceId: id,
      status: 'Cancelled',
      command: 'Cancel',
      commandId,
      executedAt: new Date().toISOString(),
      message: `Instance '${id}' cancelled successfully.`
    };
  }

  async retryInstance(
    id: string,
    mode: 'retry_failed_node' | 'retry_from_node' = 'retry_failed_node',
    fromNodeId?: string
  ): Promise<InstanceCommandResponse> {
    const commandId = crypto.randomUUID();
    const inst = this.instances.find(i => i.id === id);
    if (!inst) {
      const err = new Error(`Workflow instance '${id}' not found.`) as Error & { status?: number };
      err.status = 404;
      throw err;
    }
    if (inst.status !== 'FAULTED' && inst.status !== 'FAILED') {
      const err = new Error(`Cannot retry instance in status '${inst.status}'.`) as Error & { status?: number; code?: string };
      err.status = 409;
      err.code = 'INVALID_TRANSITION';
      throw err;
    }

    inst.status = 'RUNNING';
    this.instanceVersions[id] = (this.instanceVersions[id] ?? 1) + 1;
    this.ensureDemoEvents(inst);
    this.appendDemoEvent(id, 'Instance.RetryRequested', fromNodeId || 'node-current', { mode, fromNodeId });

    return {
      instanceId: id,
      status: 'Running',
      command: 'Retry',
      commandId,
      executedAt: new Date().toISOString(),
      message: `Instance '${id}' retry initiated successfully.`
    };
  }

  async getTasks(): Promise<ActionItemTask[]> {
    return [...this.tasks];
  }

  async completeTask(
    taskId: string,
    outcome: string,
    _comments?: string,
    formPatch?: Record<string, unknown>
  ): Promise<void> {
    this.tasks = this.tasks.map(t => {
      if (t.id === taskId) {
        return {
          ...t,
          formData: t.formMode === 'editable' && formPatch ? { ...t.formData, ...formPatch } : t.formData,
          status: outcome.toLowerCase().includes('reject') ? 'FAILED' : 'COMPLETED'
        };
      }
      return t;
    });
  }

  async getUsers(): Promise<UserRecord[]> {
    return [...this.users];
  }

  async createUser(user: Omit<UserRecord, 'id'>): Promise<UserRecord> {
    const newUser: UserRecord = {
      ...user,
      id: `usr-${Date.now()}`
    };
    this.users.push(newUser);
    return newUser;
  }

  async deleteUser(id: string): Promise<void> {
    this.users = this.users.filter(u => u.id !== id);
  }

  async getSmartObjects(): Promise<SmartObjectEntity[]> {
    return [...this.smartObjects];
  }

  async createSmartObject(entity: SmartObjectEntity): Promise<SmartObjectEntity> {
    const exists = this.smartObjects.some(s => s.id === entity.id || s.name.toLowerCase() === entity.name.toLowerCase());
    if (exists) {
      throw new Error(`SmartObject with name '${entity.name}' already exists.`);
    }
    const newEntity: SmartObjectEntity = {
      ...entity,
      recordsCount: entity.recordsCount || 0,
      fields: entity.fields || []
    };
    this.smartObjects.push(newEntity);
    if (!this.smartObjectData[newEntity.id]) {
      this.smartObjectData[newEntity.id] = [];
    }
    return newEntity;
  }

  async updateSmartObject(id: string, updates: Partial<SmartObjectEntity>): Promise<SmartObjectEntity> {
    const idx = this.smartObjects.findIndex(s => s.id === id);
    if (idx === -1) {
      throw new Error(`SmartObject with ID '${id}' not found.`);
    }
    this.smartObjects[idx] = {
      ...this.smartObjects[idx],
      ...updates,
      id // preserve ID
    };
    return this.smartObjects[idx];
  }

  async deleteSmartObject(id: string): Promise<void> {
    this.smartObjects = this.smartObjects.filter(s => s.id !== id);
    delete this.smartObjectData[id];
  }

  async getSmartObjectRecords(smartObjectId: string): Promise<SmartObjectRecord[]> {
    return [...(this.smartObjectData[smartObjectId] || [])];
  }

  async addSmartObjectRecord(smartObjectId: string, record: SmartObjectRecord): Promise<SmartObjectRecord> {
    if (!this.smartObjectData[smartObjectId]) {
      this.smartObjectData[smartObjectId] = [];
    }
    this.smartObjectData[smartObjectId].unshift(record);
    const so = this.smartObjects.find(s => s.id === smartObjectId);
    if (so) {
      so.recordsCount = this.smartObjectData[smartObjectId].length;
    }
    return record;
  }

  async updateSmartObjectRecord(smartObjectId: string, primaryKeyVal: string | number, record: SmartObjectRecord): Promise<SmartObjectRecord> {
    const list = this.smartObjectData[smartObjectId];
    if (!list) {
      throw new Error(`Data store not found for SmartObject '${smartObjectId}'.`);
    }
    const so = this.smartObjects.find(s => s.id === smartObjectId);
    const pkField = so?.fields.find(f => f.isPrimaryKey)?.name || Object.keys(record)[0] || 'id';

    const idx = list.findIndex(r => String(r[pkField]) === String(primaryKeyVal));
    if (idx === -1) {
      throw new Error(`Record with key '${primaryKeyVal}' not found.`);
    }
    list[idx] = { ...list[idx], ...record };
    return list[idx];
  }

  async deleteSmartObjectRecord(smartObjectId: string, primaryKeyVal: string | number): Promise<void> {
    const list = this.smartObjectData[smartObjectId];
    if (!list) return;
    const so = this.smartObjects.find(s => s.id === smartObjectId);
    const pkField = so?.fields.find(f => f.isPrimaryKey)?.name || 'id';

    this.smartObjectData[smartObjectId] = list.filter(r => String(r[pkField]) !== String(primaryKeyVal));
    if (so) {
      so.recordsCount = this.smartObjectData[smartObjectId].length;
    }
  }

  private settings: SystemSettingsData = {
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

  async getAnalyticsKpis(): Promise<AnalyticsKpiData> {
    const total = this.instances.length;
    return {
      avgCompletionTime: '4m 12s',
      slaComplianceRate: '98.6%',
      topBottleneckNode: 'VP Finance Sign-Off',
      topBottleneckQueueTime: '24h 10m',
      totalInstancesProcessed: total,
      volumeByDepartment: [
        { department: 'Engineering Onboarding & Access', percentage: 42, instancesCount: 612 },
        { department: 'Finance Purchase Requisitions', percentage: 31, instancesCount: 440 },
        { department: 'IT Security & Cloud Entitlements', percentage: 27, instancesCount: 380 }
      ]
    };
  }

  async getSettings(): Promise<SystemSettingsData> {
    return { ...this.settings };
  }

  async saveSettings(settings: Partial<SystemSettingsData>): Promise<SystemSettingsData> {
    this.settings = { ...this.settings, ...settings };
    return { ...this.settings };
  }
}

