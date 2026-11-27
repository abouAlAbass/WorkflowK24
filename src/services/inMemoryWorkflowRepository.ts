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
  InstanceEventRecord,
  ViewFlowNodeExecution,
  TaskAssignmentRecord,
  DelegationRuleRecord,
  CreateDelegationRulePayload
} from '../types/workflow';
import type { UserDirectoryItem, SmartObjectLookupPage } from '../types/smartform';
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

  private taskAssignments: Record<string, TaskAssignmentRecord[]> = {};
  private delegationRules: DelegationRuleRecord[] = [];

  async delegateTask(taskId: string, targetAssigneeId: string, reason?: string): Promise<void> {
    const actor = this.authSession?.user?.email || 'adel.sebti@corp.internal';
    this.tasks = this.tasks.map((t) => {
      if (t.id !== taskId) return t;
      const prev = t.assigneeId || actor;
      const entry: TaskAssignmentRecord = {
        id: Date.now(),
        taskId,
        action: 'delegation',
        fromAssigneeId: prev,
        toAssigneeId: targetAssigneeId,
        toAssigneeType: 'User',
        actorUserId: actor,
        reason: reason || null,
        createdAt: new Date().toISOString()
      };
      this.taskAssignments[taskId] = [...(this.taskAssignments[taskId] || []), entry];
      return {
        ...t,
        originalAssigneeId: t.originalAssigneeId || prev,
        assigneeId: targetAssigneeId,
        assigneeType: 'User',
        onBehalfOfUserId: t.originalAssigneeId || prev,
        version: (t.version ?? 1) + 1
      };
    });
  }

  async recallTask(taskId: string, reason?: string): Promise<void> {
    const actor = this.authSession?.user?.email || 'adel.sebti@corp.internal';
    this.tasks = this.tasks.map((t) => {
      if (t.id !== taskId) return t;
      const orig = t.originalAssigneeId || actor;
      const entry: TaskAssignmentRecord = {
        id: Date.now(),
        taskId,
        action: 'recall',
        fromAssigneeId: t.assigneeId || null,
        toAssigneeId: orig,
        toAssigneeType: 'User',
        actorUserId: actor,
        reason: reason || null,
        createdAt: new Date().toISOString()
      };
      this.taskAssignments[taskId] = [...(this.taskAssignments[taskId] || []), entry];
      return {
        ...t,
        assigneeId: orig,
        onBehalfOfUserId: null,
        claimedByUserId: null,
        version: (t.version ?? 1) + 1
      };
    });
  }

  async claimTask(taskId: string): Promise<void> {
    const actor = this.authSession?.user?.email || 'adel.sebti@corp.internal';
    this.tasks = this.tasks.map((t) => {
      if (t.id !== taskId) return t;
      const entry: TaskAssignmentRecord = {
        id: Date.now(),
        taskId,
        action: 'claim',
        fromAssigneeId: t.assigneeId || t.department,
        toAssigneeId: actor,
        toAssigneeType: 'User',
        actorUserId: actor,
        reason: 'Task claimed',
        createdAt: new Date().toISOString()
      };
      this.taskAssignments[taskId] = [...(this.taskAssignments[taskId] || []), entry];
      return {
        ...t,
        claimedByUserId: actor,
        claimedAt: new Date().toISOString(),
        version: (t.version ?? 1) + 1
      };
    });
  }

  async releaseTask(taskId: string, reason?: string): Promise<void> {
    const actor = this.authSession?.user?.email || 'adel.sebti@corp.internal';
    this.tasks = this.tasks.map((t) => {
      if (t.id !== taskId) return t;
      const entry: TaskAssignmentRecord = {
        id: Date.now(),
        taskId,
        action: 'release',
        fromAssigneeId: t.claimedByUserId || actor,
        toAssigneeId: t.assigneeId || t.department,
        toAssigneeType: t.assigneeType || 'Group',
        actorUserId: actor,
        reason: reason || 'Task released',
        createdAt: new Date().toISOString()
      };
      this.taskAssignments[taskId] = [...(this.taskAssignments[taskId] || []), entry];
      return {
        ...t,
        claimedByUserId: null,
        claimedAt: null,
        version: (t.version ?? 1) + 1
      };
    });
  }

  async getTaskAssignments(taskId: string): Promise<TaskAssignmentRecord[]> {
    return [...(this.taskAssignments[taskId] || [])];
  }

  async getDelegationRules(): Promise<DelegationRuleRecord[]> {
    return [...this.delegationRules];
  }

  async createDelegationRule(payload: CreateDelegationRulePayload): Promise<DelegationRuleRecord> {
    const delegator = this.authSession?.user?.email || 'adel.sebti@corp.internal';
    if (delegator.toLowerCase() === payload.delegateId.trim().toLowerCase()) {
      throw new Error('DELEGATION_CYCLE_DETECTED: Self-delegation creates a cycle.');
    }
    const created: DelegationRuleRecord = {
      id: `del-${Date.now()}`,
      delegatorId: delegator,
      delegateId: payload.delegateId.trim(),
      scope: payload.scope?.trim() || '*',
      validFrom: payload.validFrom,
      validTo: payload.validTo,
      active: true,
      reason: payload.reason || null,
      createdBy: delegator,
      createdAt: new Date().toISOString()
    };
    this.delegationRules = [created, ...this.delegationRules];
    return created;
  }

  async deleteDelegationRule(id: string): Promise<void> {
    this.delegationRules = this.delegationRules.filter((r) => r.id !== id);
  }

  async getUsers(): Promise<UserRecord[]> {
    return [...this.users];
  }

  async searchUsers(query = '', limit = 20): Promise<UserDirectoryItem[]> {
    const q = query.trim().toLowerCase();
    const filtered = this.users
      .filter((u) => u.status === 'Active')
      .filter((u) =>
        !q ||
        u.name.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q) ||
        u.group.toLowerCase().includes(q)
      )
      .slice(0, Math.max(1, Math.min(50, limit)));

    return filtered.map((u) => ({
      id: u.id,
      displayName: u.name,
      email: u.email,
      department: u.group
    }));
  }

  async createUser(user: Omit<UserRecord, 'id'>): Promise<UserRecord> {
    const { password: _password, ...safeUser } = user;
    const newUser: UserRecord = {
      ...safeUser,
      id: `usr-${Date.now()}`
    };
    this.users.push(newUser);
    return newUser;
  }

  async updateUser(
    id: string,
    updates: Pick<UserRecord, 'name' | 'role' | 'group' | 'status'> & { avatarUrl?: string }
  ): Promise<UserRecord> {
    const existing = this.users.find(u => u.id === id);
    if (!existing) throw new Error('User not found.');
    const updated: UserRecord = { ...existing, ...updates, avatarUrl: updates.avatarUrl ?? existing.avatarUrl };
    this.users = this.users.map(u => (u.id === id ? updated : u));
    return updated;
  }

  async resetUserPassword(_id: string, _newPassword: string): Promise<void> {
    // Demo mode has no credentials to reset.
  }

  async getUserAuditLog(): Promise<UserAuditEntry[]> {
    return [];
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

  async lookupSmartObject(
    smartObjectId: string,
    query = '',
    page = 1,
    pageSize = 25
  ): Promise<SmartObjectLookupPage> {
    const allRows = this.smartObjectData[smartObjectId] || [];
    const q = query.trim().toLowerCase();
    const filtered = !q
      ? allRows
      : allRows.filter((row) =>
          Object.values(row).some((v) => String(v ?? '').toLowerCase().includes(q))
        );

    const safePage = Math.max(1, page);
    const safePageSize = Math.max(1, Math.min(100, pageSize));
    const totalCount = filtered.length;
    const totalPages = totalCount === 0 ? 0 : Math.ceil(totalCount / safePageSize);
    const start = (safePage - 1) * safePageSize;
    const items = filtered.slice(start, start + safePageSize);

    return {
      items,
      page: safePage,
      pageSize: safePageSize,
      totalCount,
      totalPages,
      hasMore: safePage < totalPages
    };
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

  private connections: import('../types/workflow').ConnectionRecord[] = [
    {
      id: 'conn-erp-demo',
      name: 'ERP_REST_API',
      baseUrl: 'https://api.erp.example.com',
      authType: 'bearer',
      secretRef: 'env:ERP_API_TOKEN',
      hasSecretRef: true,
      allowedHosts: ['api.erp.example.com'],
      defaultHeaders: { Accept: 'application/json' },
      version: 1,
      createdBy: 'admin@corp.internal',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
  ];

  async getConnections(): Promise<import('../types/workflow').ConnectionRecord[]> {
    return [...this.connections];
  }

  async createConnection(payload: import('../types/workflow').CreateConnectionPayload): Promise<import('../types/workflow').ConnectionRecord> {
    const now = new Date().toISOString();
    const record: import('../types/workflow').ConnectionRecord = {
      id: payload.id || `conn-${Date.now().toString(36)}`,
      name: payload.name.trim(),
      baseUrl: payload.baseUrl.trim(),
      authType: payload.authType,
      secretRef: payload.secretRef?.trim() || null,
      hasSecretRef: Boolean(payload.secretRef && payload.secretRef.trim().length > 0),
      allowedHosts: payload.allowedHosts || [],
      defaultHeaders: payload.defaultHeaders || {},
      version: 1,
      createdBy: this.authSession?.user?.email || 'demo@corp.internal',
      createdAt: now,
      updatedAt: now,
      serviceType: payload.serviceType || 'rest',
      authMode: payload.authMode || 'static',
      secretSource: payload.secretSource || 'internal_encrypted',
      hasEncryptedCredentials: Boolean(payload.credentialsJson && payload.credentialsJson.trim().length > 0),
      externalSecretKey: payload.externalSecretKey || null,
      allowWrite: payload.allowWrite ?? false,
      maxRows: payload.maxRows ?? 200
    };
    this.connections = [record, ...this.connections];
    return record;
  }

  async deleteConnection(id: string): Promise<void> {
    this.connections = this.connections.filter(c => c.id !== id);
  }

  private connectionPermissions: import('../types/workflow').ConnectionPermissionRecord[] = [];
  private myCredentials: Record<string, { updatedAt: string }> = {};
  private connectionEnvironments: import('../types/workflow').ConnectionEnvironmentConfigRecord[] = [];
  private connectionUsageLogs: import('../types/workflow').ConnectionUsageLogRecord[] = [];

  async testConnection(id: string): Promise<import('../types/workflow').ConnectionTestResult> {
    const now = new Date().toISOString();
    this.connections = this.connections.map((c) =>
      c.id === id ? { ...c, healthStatus: 'Healthy', lastTestedAt: now, lastErrorSanitized: null } : c
    );
    return { success: true, latencyMs: 14 };
  }

  async rotateConnectionSecret(
    id: string,
    payload: import('../types/workflow').RotateConnectionSecretPayload
  ): Promise<import('../types/workflow').ConnectionRecord> {
    const now = new Date().toISOString();
    let updated: import('../types/workflow').ConnectionRecord | undefined;
    this.connections = this.connections.map((c) => {
      if (c.id !== id) return c;
      updated = {
        ...c,
        hasEncryptedCredentials: Boolean(payload.credentialsJson) || c.hasEncryptedCredentials,
        externalSecretKey: payload.externalSecretKey ?? c.externalSecretKey,
        lastRotatedAt: now,
        secretExpiresAt: payload.secretExpiresAt ?? c.secretExpiresAt,
        version: c.version + 1,
        updatedAt: now
      };
      return updated;
    });
    if (!updated) throw new Error(`Connection '${id}' not found.`);
    return updated;
  }

  async getConnectionUsage(
    id: string,
    limit = 50
  ): Promise<import('../types/workflow').ConnectionUsageResponse> {
    const conn = this.connections.find((c) => c.id === id);
    return {
      connectionId: id,
      connectionName: conn?.name || id,
      healthStatus: conn?.healthStatus || 'Unknown',
      lastTestedAt: conn?.lastTestedAt ?? null,
      lastUsedAt: conn?.lastUsedAt ?? null,
      lastRotatedAt: conn?.lastRotatedAt ?? null,
      secretExpiresAt: conn?.secretExpiresAt ?? null,
      lastErrorSanitized: conn?.lastErrorSanitized ?? null,
      logs: this.connectionUsageLogs.filter((l) => l.connectionId === id).slice(0, limit)
    };
  }

  async getConnectionEnvironments(
    id: string
  ): Promise<import('../types/workflow').ConnectionEnvironmentConfigRecord[]> {
    return this.connectionEnvironments.filter((e) => e.connectionId === id);
  }

  async upsertConnectionEnvironment(
    id: string,
    payload: import('../types/workflow').UpsertConnectionEnvironmentConfigPayload
  ): Promise<import('../types/workflow').ConnectionEnvironmentConfigRecord> {
    const now = new Date().toISOString();
    const existing = this.connectionEnvironments.find(
      (e) => e.connectionId === id && e.environmentName === payload.environmentName
    );
    if (existing) {
      existing.baseUrlOverride = payload.baseUrlOverride ?? null;
      if (payload.clearCredentialsOverride) {
        existing.hasEncryptedCredentialsOverride = false;
      } else if (payload.credentialsJsonOverride) {
        existing.hasEncryptedCredentialsOverride = true;
      }
      if (payload.externalSecretKeyOverride !== undefined) {
        existing.externalSecretKeyOverride = payload.externalSecretKeyOverride;
      }
      existing.updatedAt = now;
      return { ...existing };
    }
    const created: import('../types/workflow').ConnectionEnvironmentConfigRecord = {
      id: `cenv-${Date.now().toString(36)}`,
      connectionId: id,
      environmentName: payload.environmentName,
      baseUrlOverride: payload.baseUrlOverride ?? null,
      hasEncryptedCredentialsOverride: Boolean(payload.credentialsJsonOverride),
      externalSecretKeyOverride: payload.externalSecretKeyOverride ?? null,
      updatedAt: now
    };
    this.connectionEnvironments = [...this.connectionEnvironments, created];
    return created;
  }

  async getConnectionPermissions(connectionId: string): Promise<import('../types/workflow').ConnectionPermissionRecord[]> {
    return this.connectionPermissions.filter(p => p.connectionId === connectionId);
  }

  async upsertConnectionPermission(
    connectionId: string,
    payload: import('../types/workflow').UpsertConnectionPermissionPayload
  ): Promise<import('../types/workflow').ConnectionPermissionRecord> {
    const now = new Date().toISOString();
    const existing = this.connectionPermissions.find(
      p => p.connectionId === connectionId && p.subjectType === payload.subjectType && p.subjectId.toLowerCase() === payload.subjectId.toLowerCase()
    );
    if (existing) {
      existing.canManage = payload.canManage;
      existing.canUse = payload.canUse || payload.canManage;
      existing.updatedAt = now;
      return { ...existing };
    }
    const created: import('../types/workflow').ConnectionPermissionRecord = {
      id: `cperm-${Date.now().toString(36)}`,
      connectionId,
      subjectType: payload.subjectType,
      subjectId: payload.subjectId,
      canUse: payload.canUse || payload.canManage,
      canManage: payload.canManage,
      createdAt: now,
      updatedAt: now
    };
    this.connectionPermissions = [...this.connectionPermissions, created];
    return created;
  }

  async deleteConnectionPermission(connectionId: string, permissionId: string): Promise<void> {
    this.connectionPermissions = this.connectionPermissions.filter(
      p => !(p.connectionId === connectionId && p.id === permissionId)
    );
  }

  async getMyConnectionCredentials(): Promise<import('../types/workflow').UserConnectionCredentialRecord[]> {
    return this.connections
      .filter(c => c.authMode === 'user_sso_vault')
      .map(c => ({
        connectionId: c.id,
        connectionName: c.name,
        serviceType: c.serviceType || 'rest',
        authMode: c.authMode || 'user_sso_vault',
        hasCredentials: Boolean(this.myCredentials[c.id]),
        updatedAt: this.myCredentials[c.id]?.updatedAt ?? null
      }));
  }

  async upsertMyConnectionCredential(
    connectionId: string,
    _secretJson: string
  ): Promise<import('../types/workflow').UserConnectionCredentialRecord> {
    const now = new Date().toISOString();
    this.myCredentials[connectionId] = { updatedAt: now };
    const conn = this.connections.find(c => c.id === connectionId);
    return {
      connectionId,
      connectionName: conn?.name || connectionId,
      serviceType: conn?.serviceType || 'rest',
      authMode: conn?.authMode || 'user_sso_vault',
      hasCredentials: true,
      updatedAt: now
    };
  }

  async deleteMyConnectionCredential(connectionId: string): Promise<void> {
    delete this.myCredentials[connectionId];
  }

  async discoverConnectionEntities(
    _connectionId: string
  ): Promise<import('../types/workflow').DiscoveredEntity[]> {
    return [
      {
        entityName: 'employees',
        displayName: 'Employees',
        detectedPrimaryKey: 'id',
        schemaName: 'public',
        entityType: 'Table',
        fields: [
          { name: 'id', sourceType: 'int', mappedSmartObjectType: 'number', isNullable: false, isPrimaryKey: true, suggestedMasked: false },
          { name: 'full_name', sourceType: 'varchar', mappedSmartObjectType: 'string', isNullable: false, isPrimaryKey: false, suggestedMasked: false },
          { name: 'salary', sourceType: 'numeric', mappedSmartObjectType: 'number', isNullable: true, isPrimaryKey: false, suggestedMasked: true },
          { name: 'iban', sourceType: 'varchar', mappedSmartObjectType: 'string', isNullable: true, isPrimaryKey: false, suggestedMasked: true },
          { name: 'is_active', sourceType: 'boolean', mappedSmartObjectType: 'boolean', isNullable: false, isPrimaryKey: false, suggestedMasked: false }
        ]
      }
    ];
  }

  async generateSmartObjectFromConnection(
    connectionId: string,
    payload: import('../types/workflow').GenerateSmartObjectFromConnectionPayload
  ): Promise<SmartObjectEntity> {
    const conn = this.connections.find(c => c.id === connectionId);
    const hasPk = Boolean(payload.primaryKeyField && payload.primaryKeyField.trim());
    const allowWrite = Boolean(conn?.allowWrite);
    const methods: import('../types/workflow').SmartObjectMethodContract[] = [
      { name: 'Get', kind: 'read', idempotent: true },
      { name: 'List', kind: 'read', idempotent: true }
    ];
    if (allowWrite) {
      methods.push({ name: 'Create', kind: 'create', idempotent: false });
      if (hasPk) {
        methods.push({ name: 'Update', kind: 'update', idempotent: true });
        methods.push({ name: 'Delete', kind: 'delete', idempotent: true });
      }
    }
    const entity: SmartObjectEntity = {
      id: `so-${Date.now().toString(36)}`,
      name: payload.smartObjectKey,
      displayName: payload.displayName || payload.smartObjectKey,
      description: payload.description || `Imported from ${payload.entityName}`,
      category: payload.category || 'Custom',
      recordsCount: 0,
      version: 1,
      status: payload.publishImmediately ? 'Published' : 'Draft',
      sourceConnectionId: connectionId,
      sourceEntityName: payload.entityName,
      primaryKeyField: payload.primaryKeyField || null,
      methods,
      fields: payload.selectedFields.map(f => ({
        name: f.name,
        displayName: f.displayName || f.name,
        type: f.type,
        isPrimaryKey: Boolean(payload.primaryKeyField && f.name.toLowerCase() === payload.primaryKeyField.toLowerCase()),
        isRequired: Boolean(f.isRequired),
        masked: f.masked
      }))
    };
    this.smartObjects.push(entity);
    this.smartObjectData[entity.id] = [];
    return entity;
  }

  async syncSmartObjectSchema(
    smartObjectId: string,
    apply = false
  ): Promise<import('../types/workflow').SchemaDiffResult> {
    const so = this.smartObjects.find(s => s.id === smartObjectId);
    if (!so || !so.sourceConnectionId || !so.sourceEntityName) {
      throw new Error(`SmartObject '${smartObjectId}' is not linked to a Connection.`);
    }
    return {
      smartObjectId: so.id,
      connectionId: so.sourceConnectionId,
      entityName: so.sourceEntityName,
      addedFields: [],
      removedFields: [],
      typeChangedFields: [],
      primaryKeyChanged: false,
      previousPrimaryKey: so.primaryKeyField ?? null,
      newPrimaryKey: so.primaryKeyField ?? null,
      hasChanges: false,
      applied: apply
    };
  }

  private authProviders: import('../types/workflow').AuthProviderRecord[] = [];

  async getPublicAuthProviders(): Promise<import('../types/workflow').PublicAuthProvidersResponse> {
    return {
      localLoginEnabled: true,
      providers: this.authProviders
        .filter(p => p.isEnabled)
        .map(p => ({ id: p.id, name: p.name, protocol: p.protocol }))
    };
  }

  getExternalChallengeUrl(providerId: string): string {
    return `/api/auth/external/${encodeURIComponent(providerId)}/challenge`;
  }

  async exchangeExternalAuthCode(_exchangeCode: string): Promise<AuthSession> {
    throw new Error('External OIDC login requires Connected Mode (live backend API).');
  }

  async getAdminAuthProviders(): Promise<import('../types/workflow').AuthProviderRecord[]> {
    return [...this.authProviders];
  }

  async createAuthProvider(
    payload: import('../types/workflow').UpsertAuthProviderPayload
  ): Promise<import('../types/workflow').AuthProviderRecord> {
    const now = new Date().toISOString();
    const record: import('../types/workflow').AuthProviderRecord = {
      id: `authp-${Date.now().toString(36)}`,
      name: payload.name.trim(),
      protocol: payload.protocol || 'oidc',
      authority: payload.authority.trim(),
      clientId: payload.clientId.trim(),
      hasSecret: Boolean(payload.clientSecret && payload.clientSecret.trim().length > 0),
      scopes: payload.scopes?.trim() || 'openid profile email',
      roleMappingJson: payload.roleMappingJson?.trim() || '{}',
      jitProvisioningEnabled: payload.jitProvisioningEnabled ?? true,
      allowEmailMatching: payload.allowEmailMatching ?? false,
      allowAdminRoleMapping: payload.allowAdminRoleMapping ?? false,
      defaultRole: payload.defaultRole?.trim() || 'User',
      isEnabled: payload.isEnabled ?? true,
      version: 1,
      createdAt: now,
      updatedAt: now
    };
    this.authProviders = [record, ...this.authProviders];
    return record;
  }

  async updateAuthProvider(
    id: string,
    payload: import('../types/workflow').UpsertAuthProviderPayload
  ): Promise<import('../types/workflow').AuthProviderRecord> {
    const idx = this.authProviders.findIndex(p => p.id === id);
    if (idx === -1) throw new Error(`Auth provider '${id}' not found.`);
    const current = this.authProviders[idx];
    const updated: import('../types/workflow').AuthProviderRecord = {
      ...current,
      name: payload.name.trim(),
      authority: payload.authority.trim(),
      clientId: payload.clientId.trim(),
      hasSecret: payload.clearClientSecret
        ? false
        : (payload.clientSecret ? true : current.hasSecret),
      scopes: payload.scopes?.trim() || current.scopes,
      roleMappingJson: payload.roleMappingJson?.trim() || '{}',
      jitProvisioningEnabled: payload.jitProvisioningEnabled ?? current.jitProvisioningEnabled,
      allowEmailMatching: payload.allowEmailMatching ?? current.allowEmailMatching,
      allowAdminRoleMapping: payload.allowAdminRoleMapping ?? current.allowAdminRoleMapping,
      defaultRole: payload.defaultRole?.trim() || current.defaultRole,
      isEnabled: payload.isEnabled ?? current.isEnabled,
      version: current.version + 1,
      updatedAt: new Date().toISOString()
    };
    this.authProviders[idx] = updated;
    return updated;
  }

  async deleteAuthProvider(id: string): Promise<void> {
    this.authProviders = this.authProviders.filter(p => p.id !== id);
  }
}

