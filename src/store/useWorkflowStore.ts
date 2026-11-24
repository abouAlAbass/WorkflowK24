import { create } from 'zustand';
import type { 
  Node, 
  Edge, 
  OnNodesChange, 
  OnEdgesChange, 
  OnConnect 
} from '@xyflow/react';
import { 
  applyNodeChanges, 
  applyEdgeChanges, 
  addEdge 
} from '@xyflow/react';
import type { 
  WorkflowNodeCustomData, 
  WorkflowSchema, 
  SavedWorkflowRecord,
  ExecutionLog, 
  NodeType,
  ApprovalOutcome,
  NavigationTab,
  Environment,
  WorkflowInstance,
  ActionItemTask,
  SmartObjectEntity,
  SmartObjectField,
  SmartObjectRecord,
  UserRecord
} from '../types/workflow';
import { getWorkflowRepository, repositoryManager } from '../services/repositoryManager';
import type { AppMode } from '../services/workflowRepository';

const STORAGE_KEY = 'tamam_saved_workflows';

const INITIAL_INSTANCES: WorkflowInstance[] = [
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
  },
  {
    id: 'INST-9017',
    workflowName: 'Quarterly Audit Review Approval',
    templateKey: 'onboarding',
    requester: 'Rachid Boudiaf',
    requesterAvatar: '/avatars/user-5.svg',
    startedAt: '5 hours ago',
    currentStep: 'Compliance Review',
    progress: 60,
    status: 'RUNNING',
    duration: '4h 12m',
    department: 'Legal'
  }
];

const INITIAL_ACTION_TASKS: ActionItemTask[] = [
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
    ]
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
    priority: 'High',
    amount: '$17,495.00',
    description: 'Hardware refresh order for Q3 engineering leads and architects.',
    outcomes: [
      { id: 'approve', label: 'Approve PO', color: '#059669' },
      { id: 'reject', label: 'Reject PO', color: '#DC2626' }
    ]
  },
  {
    id: 'TASK-103',
    instanceId: 'INST-9017',
    title: 'Elevated AWS Prod Read Access Request',
    workflowName: 'VPN & Server Admin Access',
    requester: 'Yacine Benali',
    requesterAvatar: '/avatars/user-1.svg',
    department: 'IT Security',
    requestedAt: '2026-08-08T09:00:00Z',
    dueDate: 'Aug 10, 2026',
    status: 'PENDING',
    priority: 'Medium',
    description: 'Temporary 48-hour access request to production CloudWatch metrics & log streams.',
    outcomes: [
      { id: 'approve', label: 'Grant Access', color: '#059669' },
      { id: 'reject', label: 'Deny', color: '#DC2626' }
    ]
  }
];

const INITIAL_SMART_OBJECTS: SmartObjectEntity[] = [
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

const INITIAL_SMART_OBJECT_DATA: Record<string, SmartObjectRecord[]> = {
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

const INITIAL_USERS: UserRecord[] = [
  {
    id: 'usr-1',
    name: 'Mohamed Fetni',
    email: 'mohamed.fetni@enterprise.com',
    role: 'BPM Administrator',
    group: 'Global Admin Group',
    status: 'Active',
    avatarUrl: '/avatars/user-1.svg'
  },
  {
    id: 'usr-2',
    name: 'Adel Sebti',
    email: 'adel.sebti@enterprise.com',
    role: 'Process Manager',
    group: 'HR Managers',
    status: 'Active',
    avatarUrl: '/avatars/user-2.svg'
  },
  {
    id: 'usr-3',
    name: 'Karim Khmissi',
    email: 'karim.khmissi@enterprise.com',
    role: 'Workflow Developer',
    group: 'Finance Operations',
    status: 'Active',
    avatarUrl: '/avatars/user-3.svg'
  }
];

const getInitialSavedWorkflows = (): SavedWorkflowRecord[] => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch (err) {
    console.error('Failed to parse saved workflows from storage:', err);
    return [];
  }
};

const INITIAL_NODES: Node<WorkflowNodeCustomData>[] = [
  {
    id: 'node-start',
    type: 'trigger',
    position: { x: 100, y: 250 },
    data: {
      nodeType: 'trigger',
      label: 'New Hire Submitted',
      triggerData: {
        label: 'New Hire Form',
        triggerType: 'form',
        eventName: 'Onboarding_Submit_V1',
        variables: [
          { name: 'EmployeeName', type: 'string', defaultValue: 'Sofiane Haddad' },
          { name: 'Department', type: 'string', defaultValue: 'Engineering' },
          { name: 'Salary', type: 'number', defaultValue: '120000' },
        ],
        description: 'Fired when HR submits a new employee record form'
      }
    }
  },
  {
    id: 'node-approval-manager',
    type: 'approval',
    position: { x: 420, y: 220 },
    data: {
      nodeType: 'approval',
      label: 'Department Head Approval',
      approvalData: {
        label: 'Department Head Approval',
        assignmentType: 'group',
        assignee: 'Engineering Managers',
        allowReassignment: true,
        dueDateDays: 3,
        outcomes: [
          { id: 'outcome-approve', label: 'Approve', color: '#16a34a' },
          { id: 'outcome-reject', label: 'Reject', color: '#dc2626' },
          { id: 'outcome-info', label: 'Request Info', color: '#d97706' },
        ],
        description: 'Requires approval from the Engineering Manager group'
      }
    }
  },
  {
    id: 'node-email-welcome',
    type: 'sendEmail',
    position: { x: 800, y: 120 },
    data: {
      nodeType: 'sendEmail',
      label: 'Send Welcome Package',
      sendEmailData: {
        label: 'Send Welcome Package',
        to: '{Employee.Email}',
        subject: 'Welcome to the Team, {EmployeeName}!',
        body: 'Hi {EmployeeName},\n\nWe are excited to welcome you to the {Department} department at our company!\nYour onboarding starts next Monday.\n\nBest regards,\nHR Team',
        isHtml: true
      }
    }
  },
  {
    id: 'node-email-reject',
    type: 'sendEmail',
    position: { x: 800, y: 320 },
    data: {
      nodeType: 'sendEmail',
      label: 'Send Rejection Notice',
      sendEmailData: {
        label: 'Send Rejection Notice',
        to: 'hr-recruiting@company.com',
        subject: 'Onboarding Request Declined - {EmployeeName}',
        body: 'The onboarding request for {EmployeeName} was rejected by {Approval.Assignee}. Please review notes in HR Portal.',
        isHtml: false
      }
    }
  },
  {
    id: 'node-stage-itprovision',
    type: 'stage',
    position: { x: 1150, y: 110 },
    data: {
      nodeType: 'stage',
      label: 'IT & Hardware Provisioning Stage',
      stageData: {
        label: 'IT & Hardware Provisioning Stage',
        stageName: 'IT & Hardware Provisioning Stage',
        description: 'Multi-step provisioning sub-process for corporate email, VPN credentials, and hardware setup.',
        stageColor: '#6366F1',
        subWorkflow: {
          nodes: [
            {
              id: 'stage-step-trigger',
              type: 'trigger',
              position: { x: 100, y: 180 },
              data: {
                nodeType: 'trigger',
                label: 'Stage Initiated',
                triggerData: {
                  label: 'Stage Trigger',
                  triggerType: 'form',
                  eventName: 'Provisioning_Started',
                  variables: []
                }
              }
            },
            {
              id: 'stage-step-approval',
              type: 'approval',
              position: { x: 420, y: 160 },
              data: {
                nodeType: 'approval',
                label: 'Security Admin Approval',
                approvalData: {
                  label: 'Security Admin Approval',
                  assignmentType: 'user',
                  assignee: 'security.admin@company.com',
                  outcomes: [
                    { id: 'approve', label: 'Grant Credentials', color: '#16a34a' },
                    { id: 'reject', label: 'Deny Provisioning', color: '#dc2626' }
                  ]
                }
              }
            },
            {
              id: 'stage-step-email',
              type: 'sendEmail',
              position: { x: 780, y: 160 },
              data: {
                nodeType: 'sendEmail',
                label: 'Email VPN Keys',
                sendEmailData: {
                  label: 'Email VPN Keys',
                  to: '{Employee.Email}',
                  subject: 'Your VPN & System Credentials',
                  body: 'Welcome! Your corporate credentials and VPN profiles are ready.',
                  isHtml: false
                }
              }
            },
            {
              id: 'stage-step-end',
              type: 'end',
              position: { x: 1120, y: 180 },
              data: {
                nodeType: 'end',
                label: 'Provisioning Complete',
                endData: {
                  label: 'Provisioning Complete',
                  status: 'completed',
                  message: 'All IT provisioning tasks finished successfully.'
                }
              }
            }
          ],
          edges: [
            {
              id: 'sub-edge-1',
              source: 'stage-step-trigger',
              target: 'stage-step-approval',
              animated: true,
              style: { stroke: '#6366f1', strokeWidth: 2 }
            },
            {
              id: 'sub-edge-2',
              source: 'stage-step-approval',
              sourceHandle: 'approve',
              target: 'stage-step-email',
              label: 'Grant Credentials',
              animated: true,
              style: { stroke: '#16a34a', strokeWidth: 2 }
            },
            {
              id: 'sub-edge-3',
              source: 'stage-step-email',
              target: 'stage-step-end',
              animated: true,
              style: { stroke: '#64748b', strokeWidth: 2 }
            }
          ]
        }
      }
    }
  },
  {
    id: 'node-end-success',
    type: 'end',
    position: { x: 1560, y: 140 },
    data: {
      nodeType: 'end',
      label: 'Workflow Completed',
      endData: {
        label: 'Workflow Completed',
        status: 'completed',
        message: 'Onboarding workflow executed successfully.'
      }
    }
  },
  {
    id: 'node-end-rejected',
    type: 'end',
    position: { x: 1150, y: 340 },
    data: {
      nodeType: 'end',
      label: 'Workflow Terminated',
      endData: {
        label: 'Workflow Terminated',
        status: 'rejected',
        message: 'Process terminated due to rejection or request for info.'
      }
    }
  }
];

const INITIAL_EDGES: Edge[] = [
  {
    id: 'edge-start-approval',
    source: 'node-start',
    target: 'node-approval-manager',
    animated: true,
    style: { stroke: '#0284c7', strokeWidth: 2 }
  },
  {
    id: 'edge-approve-email',
    source: 'node-approval-manager',
    sourceHandle: 'outcome-approve',
    target: 'node-email-welcome',
    label: 'Approve',
    animated: true,
    style: { stroke: '#16a34a', strokeWidth: 2 }
  },
  {
    id: 'edge-reject-email',
    source: 'node-approval-manager',
    sourceHandle: 'outcome-reject',
    target: 'node-email-reject',
    label: 'Reject',
    animated: true,
    style: { stroke: '#dc2626', strokeWidth: 2 }
  },
  {
    id: 'edge-info-reject-merge',
    source: 'node-approval-manager',
    sourceHandle: 'outcome-info',
    target: 'node-email-reject',
    label: 'Request Info',
    animated: true,
    style: { stroke: '#d97706', strokeWidth: 2 }
  },
  {
    id: 'edge-welcome-stage',
    source: 'node-email-welcome',
    target: 'node-stage-itprovision',
    animated: true,
    style: { stroke: '#6366f1', strokeWidth: 2 }
  },
  {
    id: 'edge-stage-end',
    source: 'node-stage-itprovision',
    sourceHandle: 'stage-complete',
    target: 'node-end-success',
    label: 'Stage Complete',
    animated: true,
    style: { stroke: '#16a34a', strokeWidth: 2 }
  },
  {
    id: 'edge-reject-end',
    source: 'node-email-reject',
    target: 'node-end-rejected',
    animated: true,
    style: { stroke: '#64748b', strokeWidth: 2 }
  }
];

export interface ValidationIssue {
  id: string;
  type: 'error' | 'warning';
  nodeId?: string;
  message: string;
}

interface WorkflowState {
  theme: 'light' | 'dark';
  currentTab: NavigationTab;
  currentEnv: Environment;
  nodes: Node<WorkflowNodeCustomData>[];
  edges: Edge[];
  selectedNodeId: string | null;
  workflowName: string;
  workflowDescription: string;
  isSimulating: boolean;
  activeSimNodeId: string | null;
  simulationLogs: ExecutionLog[];
  savedWorkflows: SavedWorkflowRecord[];
  activeInstances: WorkflowInstance[];
  actionItemTasks: ActionItemTask[];
  smartObjects: SmartObjectEntity[];
  smartObjectData: Record<string, SmartObjectRecord[]>;
  users: UserRecord[];
  addUser: (user: Omit<UserRecord, 'id'>) => Promise<UserRecord>;
  deleteUser: (id: string) => Promise<void>;
  appMode: AppMode;
  isModeModalOpen: boolean;
  openModeModal: () => void;
  closeModeModal: () => void;
  syncDataFromRepository: () => Promise<void>;

  // Workflow Stage Navigation & Scope
  activeStageId: string | null;
  stageStack: Array<{ stageId: string; stageName: string; parentNodes: Node<WorkflowNodeCustomData>[]; parentEdges: Edge[] }>;
  enterStage: (stageNodeId: string) => void;
  exitStage: () => void;
  navigateToStageBreadcrumb: (index: number) => void;

  // SmartObject Entity Management
  createSmartObject: (entity: {
    name: string;
    displayName: string;
    description: string;
    category: string;
    fields?: SmartObjectField[];
  }) => Promise<SmartObjectEntity>;
  updateSmartObject: (id: string, updates: Partial<SmartObjectEntity>) => Promise<void>;
  deleteSmartObject: (id: string) => Promise<void>;
  addSmartObjectField: (smartObjectId: string, field: SmartObjectField) => void;
  deleteSmartObjectField: (smartObjectId: string, fieldName: string) => void;

  // SmartObject Data Items Management
  fetchSmartObjectRecords: (smartObjectId: string) => Promise<SmartObjectRecord[]>;
  addSmartObjectRecord: (smartObjectId: string, record: SmartObjectRecord) => Promise<void>;
  updateSmartObjectRecord: (smartObjectId: string, primaryKeyVal: string | number, record: SmartObjectRecord) => Promise<void>;
  deleteSmartObjectRecord: (smartObjectId: string, primaryKeyVal: string | number) => Promise<void>;

  // Navigation & Environment Actions
  setCurrentTab: (tab: NavigationTab) => void;
  setCurrentEnv: (env: Environment) => void;

  // Task & Instance Actions
  resolveTask: (taskId: string, outcomeId: string, comment?: string) => void;
  launchQuickWorkflow: (templateKey: 'onboarding' | 'expense' | 'itAccess') => void;

  // Store Actions
  setTheme: (theme: 'light' | 'dark') => void;
  toggleTheme: () => void;
  onNodesChange: OnNodesChange<Node<WorkflowNodeCustomData>>;
  onEdgesChange: OnEdgesChange;
  onConnect: OnConnect;
  setSelectedNodeId: (id: string | null) => void;
  setWorkflowName: (name: string) => void;
  setWorkflowDescription: (desc: string) => void;
  
  addNode: (type: NodeType, position: { x: number; y: number }) => void;
  updateNodeData: (id: string, partialData: Partial<WorkflowNodeCustomData>) => void;
  deleteNode: (id: string) => void;

  // Outcome management for Approval nodes
  addOutcomeToApproval: (nodeId: string, outcome: ApprovalOutcome) => void;
  removeOutcomeFromApproval: (nodeId: string, outcomeId: string) => void;
  updateOutcomeInApproval: (nodeId: string, outcomeId: string, updated: Partial<ApprovalOutcome>) => void;

  // Templates, Persistence & Database
  loadTemplate: (templateKey: 'onboarding' | 'expense' | 'itAccess' | 'empty') => void;
  exportWorkflowJson: () => WorkflowSchema;
  importWorkflowJson: (schema: WorkflowSchema) => void;
  saveWorkflowToDb: (customName?: string) => SavedWorkflowRecord;
  loadWorkflowFromDb: (id: string) => void;
  deleteWorkflowFromDb: (id: string) => void;
  validateWorkflow: () => ValidationIssue[];

  // Execution Simulator
  startSimulation: () => void;
  stopSimulation: () => void;
  stepSimulation: () => void;
  resetCanvas: () => void;
}

export const useWorkflowStore = create<WorkflowState>((set, get) => ({
  theme: 'light',
  currentTab: 'dashboard',
  currentEnv: 'Development',
  appMode: repositoryManager.getMode(),
  isModeModalOpen: !repositoryManager.isConfigured(),
  openModeModal: () => set({ isModeModalOpen: true }),
  closeModeModal: () => set({ isModeModalOpen: false }),
  syncDataFromRepository: async () => {
    const repo = getWorkflowRepository();
    try {
      const [instances, tasks, defs, smartObjects, users] = await Promise.all([
        repo.getInstances(),
        repo.getTasks(),
        repo.getDefinitions(),
        repo.getSmartObjects(),
        repo.getUsers()
      ]);
      set({
        activeInstances: instances,
        actionItemTasks: tasks,
        savedWorkflows: defs,
        smartObjects: smartObjects,
        users: users,
        appMode: repo.getMode()
      });
    } catch (e) {
      console.warn('Repository sync error, continuing with local state:', e);
    }
  },
  nodes: INITIAL_NODES,
  edges: INITIAL_EDGES,
  selectedNodeId: 'node-approval-manager',
  workflowName: 'Employee Onboarding Approval Workflow',
  workflowDescription: 'Tamam automated workflow for employee hiring & department head approval.',
  isSimulating: false,
  activeSimNodeId: null,
  simulationLogs: [],
  savedWorkflows: getInitialSavedWorkflows(),
  activeInstances: INITIAL_INSTANCES,
  actionItemTasks: INITIAL_ACTION_TASKS,
  smartObjects: INITIAL_SMART_OBJECTS,
  smartObjectData: INITIAL_SMART_OBJECT_DATA,
  users: INITIAL_USERS,

  activeStageId: null,
  stageStack: [],

  setCurrentTab: (tab) => set({ currentTab: tab }),
  setCurrentEnv: (env) => set({ currentEnv: env }),

  enterStage: (stageNodeId: string) => {
    const state = get();
    const stageNode = state.nodes.find(n => n.id === stageNodeId);
    if (!stageNode || stageNode.data.nodeType !== 'stage') return;

    const currentStageName = stageNode.data.stageData?.stageName || stageNode.data.label || 'Workflow Stage';

    let subWorkflow = stageNode.data.stageData?.subWorkflow;
    if (!subWorkflow || !subWorkflow.nodes || subWorkflow.nodes.length === 0) {
      const timestamp = Date.now().toString().slice(-4);
      const startId = `sub-start-${timestamp}`;
      const actionId = `sub-action-${timestamp}`;
      const endId = `sub-end-${timestamp}`;

      const defaultNodes: Node<WorkflowNodeCustomData>[] = [
        {
          id: startId,
          type: 'trigger',
          position: { x: 120, y: 180 },
          data: {
            nodeType: 'trigger',
            label: 'Stage Entry Trigger',
            triggerData: {
              label: 'Stage Entry',
              triggerType: 'form',
              eventName: `${currentStageName.replace(/\s+/g, '_')}_Start`,
              variables: []
            }
          }
        },
        {
          id: actionId,
          type: 'approval',
          position: { x: 440, y: 160 },
          data: {
            nodeType: 'approval',
            label: 'Stage Review Task',
            approvalData: {
              label: `${currentStageName} Review`,
              assignmentType: 'user',
              assignee: 'stage.reviewer@company.com',
              outcomes: [
                { id: 'approve', label: 'Approve & Next', color: '#16a34a' },
                { id: 'reject', label: 'Reject Stage', color: '#dc2626' }
              ]
            }
          }
        },
        {
          id: endId,
          type: 'end',
          position: { x: 800, y: 180 },
          data: {
            nodeType: 'end',
            label: 'Stage Complete',
            endData: {
              label: 'Stage Complete',
              status: 'completed',
              message: `${currentStageName} executed successfully.`
            }
          }
        }
      ];

      const defaultEdges: Edge[] = [
        {
          id: `edge-${startId}-${actionId}`,
          source: startId,
          target: actionId,
          animated: true,
          style: { stroke: '#6366f1', strokeWidth: 2 }
        },
        {
          id: `edge-${actionId}-${endId}`,
          source: actionId,
          sourceHandle: 'approve',
          target: endId,
          label: 'Approve & Next',
          animated: true,
          style: { stroke: '#16a34a', strokeWidth: 2 }
        }
      ];

      subWorkflow = {
        nodes: defaultNodes as any,
        edges: defaultEdges as any
      };
    }

    const newStackItem = {
      stageId: stageNodeId,
      stageName: currentStageName,
      parentNodes: state.nodes,
      parentEdges: state.edges
    };

    set({
      stageStack: [...state.stageStack, newStackItem],
      activeStageId: stageNodeId,
      nodes: subWorkflow.nodes as Node<WorkflowNodeCustomData>[],
      edges: subWorkflow.edges as Edge[],
      selectedNodeId: null
    });
  },

  exitStage: () => {
    const state = get();
    if (state.stageStack.length === 0) return;

    const lastStackItem = state.stageStack[state.stageStack.length - 1];
    const newStack = state.stageStack.slice(0, -1);
    const nextActiveStageId = newStack.length > 0 ? newStack[newStack.length - 1].stageId : null;

    const updatedParentNodes = lastStackItem.parentNodes.map(node => {
      if (node.id === lastStackItem.stageId) {
        return {
          ...node,
          data: {
            ...node.data,
            stageData: {
              ...node.data.stageData!,
              subWorkflow: {
                nodes: state.nodes,
                edges: state.edges
              }
            }
          }
        };
      }
      return node;
    });

    set({
      stageStack: newStack,
      activeStageId: nextActiveStageId,
      nodes: updatedParentNodes,
      edges: lastStackItem.parentEdges,
      selectedNodeId: lastStackItem.stageId
    });
  },

  navigateToStageBreadcrumb: (index: number) => {
    const state = get();
    if (index === -1) {
      if (state.stageStack.length === 0) return;
      let currentNodes = state.nodes;
      let currentEdges = state.edges;
      const stack = [...state.stageStack];

      while (stack.length > 0) {
        const top = stack.pop()!;
        currentNodes = top.parentNodes.map(node => {
          if (node.id === top.stageId) {
            return {
              ...node,
              data: {
                ...node.data,
                stageData: {
                  ...node.data.stageData!,
                  subWorkflow: {
                    nodes: currentNodes,
                    edges: currentEdges
                  }
                }
              }
            };
          }
          return node;
        });
        currentEdges = top.parentEdges;
      }

      set({
        stageStack: [],
        activeStageId: null,
        nodes: currentNodes,
        edges: currentEdges,
        selectedNodeId: null
      });
      return;
    }

    if (index >= state.stageStack.length - 1) return;

    let currentNodes = state.nodes;
    let currentEdges = state.edges;
    const stack = [...state.stageStack];

    while (stack.length > index + 1) {
      const top = stack.pop()!;
      currentNodes = top.parentNodes.map(node => {
        if (node.id === top.stageId) {
          return {
            ...node,
            data: {
              ...node.data,
              stageData: {
                ...node.data.stageData!,
                subWorkflow: {
                  nodes: currentNodes,
                  edges: currentEdges
                }
              }
            }
          };
        }
        return node;
      });
      currentEdges = top.parentEdges;
    }

    const nextActiveStageId = stack[stack.length - 1]?.stageId || null;
    set({
      stageStack: stack,
      activeStageId: nextActiveStageId,
      nodes: currentNodes,
      edges: currentEdges,
      selectedNodeId: null
    });
  },

  resolveTask: (taskId, outcomeId, comment) => {
    const task = get().actionItemTasks.find(t => t.id === taskId);
    if (!task) return;

    const repo = getWorkflowRepository();
    repo.completeTask(taskId, outcomeId, 'Current User', comment).catch((err: any) => {
      console.warn('Repository task completion error:', err);
    });

    set((state) => ({
      actionItemTasks: state.actionItemTasks.filter(t => t.id !== taskId),
      activeInstances: state.activeInstances.map(inst => {
        if (inst.id === task.instanceId) {
          return {
            ...inst,
            status: outcomeId === 'reject' ? 'FAILED' : 'COMPLETED',
            progress: 100,
            currentStep: outcomeId === 'reject' ? 'Rejected by Manager' : 'Completed Successfully'
          };
        }
        return inst;
      }),
      simulationLogs: [
        {
          timestamp: new Date().toLocaleTimeString(),
          nodeId: taskId,
          nodeName: task.title,
          status: outcomeId === 'reject' ? 'error' : 'success',
          message: `Decision outcome "${outcomeId.toUpperCase()}" logged for ${task.instanceId}.${comment ? ` Comment: "${comment}"` : ''}`
        },
        ...state.simulationLogs
      ]
    }));
  },

  launchQuickWorkflow: (templateKey) => {
    get().loadTemplate(templateKey);
    const newId = `INST-${Math.floor(1000 + Math.random() * 9000)}`;
    const titles = {
      onboarding: 'Employee Onboarding Process',
      expense: 'Purchase Request ($3,200)',
      itAccess: 'System & AD Access Request'
    };
    const newInst: WorkflowInstance = {
      id: newId,
      workflowName: titles[templateKey] || 'Custom Workflow',
      templateKey: templateKey,
      requester: 'Mohamed Fetni',
      requesterAvatar: '/avatars/user-1.svg',
      startedAt: 'Just now',
      currentStep: 'Trigger Fired',
      progress: 25,
      status: 'RUNNING',
      duration: '0m 05s',
      department: 'Operations'
    };

    set((state) => ({
      activeInstances: [newInst, ...state.activeInstances],
      currentTab: 'designer',
      simulationLogs: [
        {
          timestamp: new Date().toLocaleTimeString(),
          nodeId: newId,
          nodeName: newInst.workflowName,
          status: 'info',
          message: `Workflow Instance ${newId} initiated by Mohamed Fetni.`
        },
        ...state.simulationLogs
      ]
    }));
  },

  setTheme: (theme) => set({ theme }),

  toggleTheme: () => set((state) => ({ theme: state.theme === 'light' ? 'dark' : 'light' })),

  onNodesChange: (changes) => {
    set({
      nodes: applyNodeChanges(changes, get().nodes)
    });
  },

  onEdgesChange: (changes) => {
    set({
      edges: applyEdgeChanges(changes, get().edges)
    });
  },

  onConnect: (connection) => {
    const sourceNode = get().nodes.find(n => n.id === connection.source);
    let label = '';
    let strokeColor = '#0284c7';

    if (sourceNode?.data.nodeType === 'approval' && sourceNode.data.approvalData && connection.sourceHandle) {
      const outcome = sourceNode.data.approvalData.outcomes.find(o => o.id === connection.sourceHandle);
      if (outcome) {
        label = outcome.label;
        strokeColor = outcome.color;
      }
    } else if (sourceNode?.data.nodeType === 'decision' && sourceNode.data.decisionData && connection.sourceHandle) {
      if (connection.sourceHandle === 'decision-true') {
        label = sourceNode.data.decisionData.trueOutcomeLabel || 'True';
        strokeColor = '#16a34a';
      } else {
        label = sourceNode.data.decisionData.falseOutcomeLabel || 'False';
        strokeColor = '#dc2626';
      }
    }

    const newEdge: Edge = {
      ...connection,
      id: `edge-${connection.source}-${connection.sourceHandle || 'default'}-${connection.target}-${Date.now()}`,
      animated: true,
      label: label,
      style: { stroke: strokeColor, strokeWidth: 2 }
    };

    set({
      edges: addEdge(newEdge, get().edges)
    });
  },

  setSelectedNodeId: (id) => set({ selectedNodeId: id }),

  setWorkflowName: (name) => set({ workflowName: name }),

  setWorkflowDescription: (desc) => set({ workflowDescription: desc }),

  addNode: (type, position) => {
    const newId = `node-${type}-${Date.now().toString().slice(-4)}`;
    
    let nodeData: WorkflowNodeCustomData = {
      nodeType: type,
      label: `${type.charAt(0).toUpperCase() + type.slice(1)} Node`
    };

    if (type === 'trigger') {
      nodeData.label = 'New Event Trigger';
      nodeData.triggerData = {
        label: 'Form Submission',
        triggerType: 'form',
        eventName: 'New_Record_Trigger',
        variables: [{ name: 'RequesterEmail', type: 'string', defaultValue: 'user@company.com' }],
        description: 'Triggers workflow execution'
      };
    } else if (type === 'approval') {
      nodeData.label = 'Manager Approval Task';
      nodeData.approvalData = {
        label: 'Manager Approval Task',
        assignmentType: 'user',
        assignee: 'mohamed.fetni@company.com',
        allowReassignment: true,
        dueDateDays: 2,
        outcomes: [
          { id: `outcome-approve-${Date.now()}`, label: 'Approve', color: '#16a34a' },
          { id: `outcome-reject-${Date.now()}`, label: 'Reject', color: '#dc2626' }
        ],
        description: 'User or Group decision task'
      };
    } else if (type === 'sendEmail') {
      nodeData.label = 'Send Email Notification';
      nodeData.sendEmailData = {
        label: 'Send Email Notification',
        to: '{RequesterEmail}',
        subject: 'Notification from Tamam Workflow',
        body: 'Hello,\n\nYour workflow step has been processed.',
        isHtml: false
      };
    } else if (type === 'end') {
      nodeData.label = 'End Workflow';
      nodeData.endData = {
        label: 'End Workflow',
        status: 'completed',
        message: 'Workflow finished.'
      };
    } else if (type === 'decision') {
      nodeData.label = 'Check Amount Decision';
      nodeData.decisionData = {
        label: 'Check Amount Decision',
        conditions: [
          { id: 'c1', field: 'Amount', operator: 'greater_than', value: '5000' }
        ],
        trueOutcomeLabel: 'Yes (> $5k)',
        falseOutcomeLabel: 'No (<= $5k)'
      };
    } else if (type === 'stage') {
      const timestamp = Date.now().toString().slice(-4);
      const stageName = 'New Workflow Stage';
      const startId = `sub-start-${timestamp}`;
      const actionId = `sub-action-${timestamp}`;
      const endId = `sub-end-${timestamp}`;

      const defaultSubNodes: Node<WorkflowNodeCustomData>[] = [
        {
          id: startId,
          type: 'trigger',
          position: { x: 120, y: 180 },
          data: {
            nodeType: 'trigger',
            label: 'Stage Entry Trigger',
            triggerData: {
              label: 'Stage Entry',
              triggerType: 'form',
              eventName: 'Stage_Started',
              variables: []
            }
          }
        },
        {
          id: actionId,
          type: 'approval',
          position: { x: 440, y: 160 },
          data: {
            nodeType: 'approval',
            label: 'Stage Action Task',
            approvalData: {
              label: 'Stage Action Task',
              assignmentType: 'user',
              assignee: 'stage.lead@company.com',
              outcomes: [
                { id: 'approve', label: 'Approve & Proceed', color: '#16a34a' },
                { id: 'reject', label: 'Reject / Terminate', color: '#dc2626' }
              ]
            }
          }
        },
        {
          id: endId,
          type: 'end',
          position: { x: 800, y: 180 },
          data: {
            nodeType: 'end',
            label: 'Stage Completed',
            endData: {
              label: 'Stage Completed',
              status: 'completed',
              message: 'Stage executed successfully.'
            }
          }
        }
      ];

      const defaultSubEdges: Edge[] = [
        {
          id: `edge-${startId}-${actionId}`,
          source: startId,
          target: actionId,
          animated: true,
          style: { stroke: '#6366f1', strokeWidth: 2 }
        },
        {
          id: `edge-${actionId}-${endId}`,
          source: actionId,
          sourceHandle: 'approve',
          target: endId,
          label: 'Approve & Proceed',
          animated: true,
          style: { stroke: '#16a34a', strokeWidth: 2 }
        }
      ];

      nodeData.label = stageName;
      nodeData.stageData = {
        label: stageName,
        stageName: stageName,
        description: 'Composite sub-process containing its own isolated workflow logic.',
        stageColor: '#6366F1',
        subWorkflow: {
          nodes: defaultSubNodes,
          edges: defaultSubEdges
        }
      };
    }

    const newNode: Node<WorkflowNodeCustomData> = {
      id: newId,
      type: type,
      position,
      data: nodeData
    };

    set({
      nodes: [...get().nodes, newNode],
      selectedNodeId: newId
    });
  },

  updateNodeData: (id, partialData) => {
    set({
      nodes: get().nodes.map(node => {
        if (node.id === id) {
          return {
            ...node,
            data: {
              ...node.data,
              ...partialData
            }
          };
        }
        return node;
      })
    });
  },

  deleteNode: (id) => {
    set({
      nodes: get().nodes.filter(n => n.id !== id),
      edges: get().edges.filter(e => e.source !== id && e.target !== id),
      selectedNodeId: get().selectedNodeId === id ? null : get().selectedNodeId
    });
  },

  addOutcomeToApproval: (nodeId, outcome) => {
    set({
      nodes: get().nodes.map(node => {
        if (node.id === nodeId && node.data.approvalData) {
          return {
            ...node,
            data: {
              ...node.data,
              approvalData: {
                ...node.data.approvalData,
                outcomes: [...node.data.approvalData.outcomes, outcome]
              }
            }
          };
        }
        return node;
      })
    });
  },

  removeOutcomeFromApproval: (nodeId, outcomeId) => {
    set({
      nodes: get().nodes.map(node => {
        if (node.id === nodeId && node.data.approvalData) {
          return {
            ...node,
            data: {
              ...node.data,
              approvalData: {
                ...node.data.approvalData,
                outcomes: node.data.approvalData.outcomes.filter(o => o.id !== outcomeId)
              }
            }
          };
        }
        return node;
      }),
      edges: get().edges.filter(e => !(e.source === nodeId && e.sourceHandle === outcomeId))
    });
  },

  updateOutcomeInApproval: (nodeId, outcomeId, updated) => {
    set({
      nodes: get().nodes.map(node => {
        if (node.id === nodeId && node.data.approvalData) {
          const updatedOutcomes = node.data.approvalData.outcomes.map(o => {
            if (o.id === outcomeId) {
              return { ...o, ...updated };
            }
            return o;
          });
          return {
            ...node,
            data: {
              ...node.data,
              approvalData: {
                ...node.data.approvalData,
                outcomes: updatedOutcomes
              }
            }
          };
        }
        return node;
      }),
      edges: get().edges.map(edge => {
        if (edge.source === nodeId && edge.sourceHandle === outcomeId && updated.label) {
          return {
            ...edge,
            label: updated.label,
            style: { ...edge.style, stroke: updated.color || edge.style?.stroke }
          };
        }
        return edge;
      })
    });
  },

  loadTemplate: (templateKey) => {
    if (templateKey === 'onboarding') {
      set({
        nodes: INITIAL_NODES,
        edges: INITIAL_EDGES,
        workflowName: 'Employee Onboarding Approval Workflow',
        workflowDescription: 'Tamam automated workflow for employee hiring & department head approval.',
        selectedNodeId: 'node-approval-manager'
      });
    } else if (templateKey === 'expense') {
      set({
        workflowName: 'Executive Purchase Request Workflow',
        workflowDescription: 'Multi-level approval process with dynamic amount branching.',
        nodes: [
          {
            id: 'exp-start',
            type: 'trigger',
            position: { x: 100, y: 200 },
            data: {
              nodeType: 'trigger',
              label: 'Expense Submitted',
              triggerData: {
                label: 'Expense Form',
                triggerType: 'form',
                eventName: 'Expense_Submission',
                variables: [
                  { name: 'Amount', type: 'number', defaultValue: '7500' },
                  { name: 'Category', type: 'string', defaultValue: 'Software License' }
                ]
              }
            }
          },
          {
            id: 'exp-decision',
            type: 'decision',
            position: { x: 380, y: 190 },
            data: {
              nodeType: 'decision',
              label: 'Amount > $5,000?',
              decisionData: {
                label: 'Amount > $5,000?',
                conditions: [{ id: '1', field: 'Amount', operator: 'greater_than', value: '5000' }],
                trueOutcomeLabel: 'Yes (> $5k)',
                falseOutcomeLabel: 'No (Auto Approve)'
              }
            }
          },
          {
            id: 'exp-vp-approval',
            type: 'approval',
            position: { x: 700, y: 100 },
            data: {
              nodeType: 'approval',
              label: 'VP Finance Approval',
              approvalData: {
                label: 'VP Finance Approval',
                assignmentType: 'group',
                assignee: 'Finance Executives',
                outcomes: [
                  { id: 'vp-approve', label: 'Approve Payment', color: '#16a34a' },
                  { id: 'vp-reject', label: 'Deny Request', color: '#dc2626' }
                ]
              }
            }
          },
          {
            id: 'exp-email-notify',
            type: 'sendEmail',
            position: { x: 1020, y: 120 },
            data: {
              nodeType: 'sendEmail',
              label: 'Notify Accounts Payable',
              sendEmailData: {
                label: 'Notify Accounts Payable',
                to: 'ap@company.com',
                subject: 'Payment Approved - ${Amount}',
                body: 'The purchase of ${Category} for ${Amount} was approved.'
              }
            }
          },
          {
            id: 'exp-end',
            type: 'end',
            position: { x: 1320, y: 220 },
            data: {
              nodeType: 'end',
              label: 'Expense Processed',
              endData: {
                label: 'Expense Processed',
                status: 'completed',
                message: 'Expense workflow completed.'
              }
            }
          }
        ],
        edges: [
          { id: 'e1', source: 'exp-start', target: 'exp-decision', animated: true, style: { stroke: '#0284c7', strokeWidth: 2 } },
          { id: 'e2', source: 'exp-decision', sourceHandle: 'decision-true', target: 'exp-vp-approval', label: 'Yes (> $5k)', animated: true, style: { stroke: '#16a34a', strokeWidth: 2 } },
          { id: 'e3', source: 'exp-decision', sourceHandle: 'decision-false', target: 'exp-end', label: 'No (Auto Approve)', animated: true, style: { stroke: '#64748b', strokeWidth: 2 } },
          { id: 'e4', source: 'exp-vp-approval', sourceHandle: 'vp-approve', target: 'exp-email-notify', label: 'Approve Payment', animated: true, style: { stroke: '#16a34a', strokeWidth: 2 } },
          { id: 'e5', source: 'exp-email-notify', target: 'exp-end', animated: true, style: { stroke: '#64748b', strokeWidth: 2 } }
        ],
        selectedNodeId: 'exp-decision'
      });
    } else if (templateKey === 'itAccess') {
      set({
        workflowName: 'IT System Access Provisioning',
        workflowDescription: 'Automated role-based access request workflow.',
        nodes: [
          {
            id: 'it-start',
            type: 'trigger',
            position: { x: 100, y: 200 },
            data: {
              nodeType: 'trigger',
              label: 'Access Requested',
              triggerData: { label: 'Access Portal', triggerType: 'webhook', eventName: 'Access_Grant_Event', variables: [] }
            }
          },
          {
            id: 'it-approval',
            type: 'approval',
            position: { x: 420, y: 180 },
            data: {
              nodeType: 'approval',
              label: 'Security Admin Review',
              approvalData: {
                label: 'Security Admin Review',
                assignmentType: 'group',
                assignee: 'IT Security Officers',
                outcomes: [
                  { id: 'it-grant', label: 'Grant Access', color: '#16a34a' },
                  { id: 'it-deny', label: 'Deny Access', color: '#dc2626' }
                ]
              }
            }
          },
          {
            id: 'it-end',
            type: 'end',
            position: { x: 800, y: 200 },
            data: {
              nodeType: 'end',
              label: 'Provisioning Done',
              endData: { label: 'Provisioning Done', status: 'completed', message: 'Access request handled.' }
            }
          }
        ],
        edges: [
          { id: 'it-e1', source: 'it-start', target: 'it-approval', animated: true, style: { stroke: '#0284c7', strokeWidth: 2 } },
          { id: 'it-e2', source: 'it-approval', sourceHandle: 'it-grant', target: 'it-end', label: 'Grant Access', animated: true, style: { stroke: '#16a34a', strokeWidth: 2 } }
        ],
        selectedNodeId: 'it-approval'
      });
    } else {
      set({
        nodes: [],
        edges: [],
        workflowName: 'Untitled Workflow',
        workflowDescription: '',
        selectedNodeId: null
      });
    }
  },

  exportWorkflowJson: () => {
    const state = get();
    let rootNodes = state.nodes;
    let rootEdges = state.edges;

    if (state.stageStack.length > 0) {
      let currentNodes = state.nodes;
      let currentEdges = state.edges;
      const stack = [...state.stageStack];
      while (stack.length > 0) {
        const top = stack.pop()!;
        currentNodes = top.parentNodes.map(node => {
          if (node.id === top.stageId) {
            return {
              ...node,
              data: {
                ...node.data,
                stageData: {
                  ...node.data.stageData!,
                  subWorkflow: {
                    nodes: currentNodes,
                    edges: currentEdges
                  }
                }
              }
            };
          }
          return node;
        });
        currentEdges = top.parentEdges;
      }
      rootNodes = currentNodes;
      rootEdges = currentEdges;
    }

    return {
      id: `wf-${Date.now()}`,
      name: state.workflowName,
      version: '1.0.0',
      description: state.workflowDescription,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      nodes: rootNodes.map(n => ({
        id: n.id,
        type: n.type || 'trigger',
        position: n.position,
        data: n.data
      })),
      edges: rootEdges.map(e => ({
        id: e.id,
        source: e.source,
        target: e.target,
        sourceHandle: e.sourceHandle || null,
        targetHandle: e.targetHandle || null,
        label: e.label as string || ''
      }))
    };
  },

  importWorkflowJson: (schema) => {
    try {
      const importedNodes: Node<WorkflowNodeCustomData>[] = schema.nodes.map(n => ({
        id: n.id,
        type: n.type,
        position: n.position,
        data: n.data
      }));

      const importedEdges: Edge[] = schema.edges.map(e => ({
        id: e.id,
        source: e.source,
        target: e.target,
        sourceHandle: e.sourceHandle || undefined,
        targetHandle: e.targetHandle || undefined,
        label: e.label || '',
        animated: true,
        style: { stroke: '#0284c7', strokeWidth: 2 }
      }));

      set({
        workflowName: schema.name || 'Imported Workflow',
        workflowDescription: schema.description || '',
        nodes: importedNodes,
        edges: importedEdges,
        selectedNodeId: importedNodes[0]?.id || null
      });
    } catch (err) {
      console.error('Failed to import workflow schema:', err);
    }
  },

  saveWorkflowToDb: (customName) => {
    const schema = get().exportWorkflowJson();
    if (customName) {
      schema.name = customName;
    }
    const record: SavedWorkflowRecord = {
      id: schema.id,
      name: schema.name,
      savedAt: new Date().toLocaleTimeString() + ', ' + new Date().toLocaleDateString(),
      schema
    };

    const existing = get().savedWorkflows;
    const updated = [record, ...existing.filter(r => r.name !== schema.name)];
    
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
    } catch (e) {
      console.error('LocalStorage save error:', e);
    }

    const repo = getWorkflowRepository();
    repo.saveDefinition(record).catch((err: any) => {
      console.warn('Repository save definition error:', err);
    });

    set({ savedWorkflows: updated });
    return record;
  },

  loadWorkflowFromDb: (id) => {
    const record = get().savedWorkflows.find(r => r.id === id);
    if (record) {
      get().importWorkflowJson(record.schema);
    }
  },

  deleteWorkflowFromDb: (id) => {
    const updated = get().savedWorkflows.filter(r => r.id !== id);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
    } catch (e) {
      console.error('LocalStorage delete error:', e);
    }

    const repo = getWorkflowRepository();
    repo.deleteDefinition(id).catch((err: any) => {
      console.warn('Repository delete definition error:', err);
    });

    set({ savedWorkflows: updated });
  },

  validateWorkflow: () => {
    const { nodes, edges } = get();
    const issues: ValidationIssue[] = [];

    const triggerNodes = nodes.filter(n => n.data.nodeType === 'trigger');
    if (triggerNodes.length === 0) {
      issues.push({
        id: 'val-no-trigger',
        type: 'error',
        message: 'Workflow must have at least one Start/Trigger node.'
      });
    }

    const endNodes = nodes.filter(n => n.data.nodeType === 'end');
    if (endNodes.length === 0) {
      issues.push({
        id: 'val-no-end',
        type: 'error',
        message: 'Workflow must have at least one End Event node.'
      });
    }

    nodes.forEach(node => {
      const hasIncoming = edges.some(e => e.target === node.id);
      const hasOutgoing = edges.some(e => e.source === node.id);

      if (node.data.nodeType !== 'trigger' && !hasIncoming) {
        issues.push({
          id: `val-unreachable-${node.id}`,
          type: 'warning',
          nodeId: node.id,
          message: `Node "${node.data.label}" is unreachable (no incoming connections).`
        });
      }

      if (node.data.nodeType !== 'end' && !hasOutgoing) {
        issues.push({
          id: `val-deadend-${node.id}`,
          type: 'warning',
          nodeId: node.id,
          message: `Node "${node.data.label}" is a dead end (no outbound connections).`
        });
      }

      if (node.data.nodeType === 'approval' && node.data.approvalData) {
        if (!node.data.approvalData.assignee.trim()) {
          issues.push({
            id: `val-assignee-${node.id}`,
            type: 'error',
            nodeId: node.id,
            message: `Approval Task "${node.data.label}" is missing an Assignee User/Group.`
          });
        }
        if (!node.data.approvalData.outcomes || node.data.approvalData.outcomes.length === 0) {
          issues.push({
            id: `val-outcomes-${node.id}`,
            type: 'error',
            nodeId: node.id,
            message: `Approval Task "${node.data.label}" must have at least 1 decision outcome.`
          });
        }
      }

      if (node.data.nodeType === 'sendEmail' && node.data.sendEmailData) {
        if (!node.data.sendEmailData.to.trim()) {
          issues.push({
            id: `val-email-to-${node.id}`,
            type: 'error',
            nodeId: node.id,
            message: `Send Email node "${node.data.label}" is missing Recipient (To).`
          });
        }
      }
    });

    return issues;
  },

  startSimulation: () => {
    const { nodes } = get();
    const startNode = nodes.find(n => n.data.nodeType === 'trigger');
    if (!startNode) return;

    set({
      isSimulating: true,
      activeSimNodeId: startNode.id,
      simulationLogs: [
        {
          timestamp: new Date().toLocaleTimeString(),
          nodeId: startNode.id,
          nodeName: startNode.data.label,
          status: 'info',
          message: `Workflow started by event "${startNode.data.triggerData?.eventName || 'Form Submit'}"`
        }
      ]
    });
  },

  stopSimulation: () => {
    set({
      isSimulating: false,
      activeSimNodeId: null
    });
  },

  stepSimulation: () => {
    const { activeSimNodeId, nodes, edges, simulationLogs } = get();
    if (!activeSimNodeId) return;

    const currentNode = nodes.find(n => n.id === activeSimNodeId);
    if (!currentNode) return;

    const outgoingEdges = edges.filter(e => e.source === activeSimNodeId);
    
    if (outgoingEdges.length === 0 || currentNode.data.nodeType === 'end') {
      set({
        activeSimNodeId: null,
        isSimulating: false,
        simulationLogs: [
          ...simulationLogs,
          {
            timestamp: new Date().toLocaleTimeString(),
            nodeId: currentNode.id,
            nodeName: currentNode.data.label,
            status: 'success',
            message: `Workflow execution completed at "${currentNode.data.label}".`
          }
        ]
      });
      return;
    }

    const nextEdge = outgoingEdges[0];
    const nextNode = nodes.find(n => n.id === nextEdge.target);

    if (nextNode) {
      set({
        activeSimNodeId: nextNode.id,
        simulationLogs: [
          ...simulationLogs,
          {
            timestamp: new Date().toLocaleTimeString(),
            nodeId: nextNode.id,
            nodeName: nextNode.data.label,
            status: 'info',
            message: `Transitioned via path "${nextEdge.label || 'Default'}" to "${nextNode.data.label}".`,
            outcome: nextEdge.label as string
          }
        ]
      });
    }
  },

  // SmartObject Entity & Records Actions
  createSmartObject: async (entityData) => {
    const newId = `so-${entityData.name.toLowerCase().replace(/[^a-z0-9]/g, '-')}-${Date.now().toString().slice(-4)}`;
    const fields: SmartObjectField[] = entityData.fields && entityData.fields.length > 0
      ? entityData.fields
      : [{ name: 'Id', type: 'GUID', isPrimaryKey: true, isRequired: true }];

    const newEntity: SmartObjectEntity = {
      id: newId,
      name: entityData.name,
      displayName: entityData.displayName || entityData.name,
      description: entityData.description || '',
      category: entityData.category || 'Custom',
      recordsCount: 0,
      fields
    };

    set((state) => ({
      smartObjects: [newEntity, ...state.smartObjects],
      smartObjectData: {
        ...state.smartObjectData,
        [newId]: []
      }
    }));

    try {
      const repo = getWorkflowRepository();
      await repo.createSmartObject(newEntity);
    } catch (err) {
      console.warn('Repository createSmartObject error:', err);
    }

    return newEntity;
  },

  updateSmartObject: async (id, updates) => {
    set((state) => ({
      smartObjects: state.smartObjects.map((so) =>
        so.id === id ? { ...so, ...updates } : so
      )
    }));

    try {
      const repo = getWorkflowRepository();
      await repo.updateSmartObject(id, updates);
    } catch (err) {
      console.warn('Repository updateSmartObject error:', err);
    }
  },

  deleteSmartObject: async (id) => {
    set((state) => {
      const nextData = { ...state.smartObjectData };
      delete nextData[id];
      return {
        smartObjects: state.smartObjects.filter((so) => so.id !== id),
        smartObjectData: nextData
      };
    });

    try {
      const repo = getWorkflowRepository();
      await repo.deleteSmartObject(id);
    } catch (err) {
      console.warn('Repository deleteSmartObject error:', err);
    }
  },

  addSmartObjectField: (smartObjectId, field) => {
    set((state) => ({
      smartObjects: state.smartObjects.map((so) => {
        if (so.id !== smartObjectId) return so;
        const updatedExisting = field.isPrimaryKey
          ? so.fields.map(f => ({ ...f, isPrimaryKey: false }))
          : so.fields;
        return {
          ...so,
          fields: [...updatedExisting, field]
        };
      })
    }));

    const so = get().smartObjects.find(s => s.id === smartObjectId);
    if (so) {
      getWorkflowRepository().updateSmartObject(smartObjectId, { fields: so.fields }).catch(console.warn);
    }
  },

  deleteSmartObjectField: (smartObjectId, fieldName) => {
    set((state) => ({
      smartObjects: state.smartObjects.map((so) => {
        if (so.id !== smartObjectId) return so;
        return {
          ...so,
          fields: so.fields.filter(f => f.name !== fieldName)
        };
      })
    }));

    const so = get().smartObjects.find(s => s.id === smartObjectId);
    if (so) {
      getWorkflowRepository().updateSmartObject(smartObjectId, { fields: so.fields }).catch(console.warn);
    }
  },

  fetchSmartObjectRecords: async (smartObjectId) => {
    try {
      const repo = getWorkflowRepository();
      const records = await repo.getSmartObjectRecords(smartObjectId);
      set((state) => ({
        smartObjectData: {
          ...state.smartObjectData,
          [smartObjectId]: records
        },
        smartObjects: state.smartObjects.map((so) =>
          so.id === smartObjectId ? { ...so, recordsCount: records.length } : so
        )
      }));
      return records;
    } catch (err) {
      console.warn('Repository fetchSmartObjectRecords error:', err);
      return get().smartObjectData[smartObjectId] || [];
    }
  },

  addSmartObjectRecord: async (smartObjectId, record) => {
    set((state) => {
      const currentRecords = state.smartObjectData[smartObjectId] || [];
      const updated = [record, ...currentRecords];
      return {
        smartObjectData: {
          ...state.smartObjectData,
          [smartObjectId]: updated
        },
        smartObjects: state.smartObjects.map((so) =>
          so.id === smartObjectId ? { ...so, recordsCount: updated.length } : so
        )
      };
    });

    try {
      const repo = getWorkflowRepository();
      await repo.addSmartObjectRecord(smartObjectId, record);
    } catch (err) {
      console.warn('Repository addSmartObjectRecord error:', err);
    }
  },

  updateSmartObjectRecord: async (smartObjectId, primaryKeyVal, updatedRecord) => {
    const so = get().smartObjects.find(s => s.id === smartObjectId);
    const pkField = so?.fields.find(f => f.isPrimaryKey)?.name || 'id';

    set((state) => {
      const currentRecords = state.smartObjectData[smartObjectId] || [];
      const updated = currentRecords.map((r) => {
        const rowPk = r[pkField] ?? r.id ?? r.Id ?? r.ID;
        if (String(rowPk) === String(primaryKeyVal)) {
          return { ...r, ...updatedRecord };
        }
        return r;
      });
      return {
        smartObjectData: {
          ...state.smartObjectData,
          [smartObjectId]: updated
        }
      };
    });

    try {
      const repo = getWorkflowRepository();
      await repo.updateSmartObjectRecord(smartObjectId, primaryKeyVal, updatedRecord);
    } catch (err) {
      console.warn('Repository updateSmartObjectRecord error:', err);
    }
  },

  deleteSmartObjectRecord: async (smartObjectId, primaryKeyVal) => {
    const so = get().smartObjects.find(s => s.id === smartObjectId);
    const pkField = so?.fields.find(f => f.isPrimaryKey)?.name || 'id';

    set((state) => {
      const currentRecords = state.smartObjectData[smartObjectId] || [];
      const updated = currentRecords.filter((r) => {
        const rowPk = r[pkField] ?? r.id ?? r.Id ?? r.ID;
        return String(rowPk) !== String(primaryKeyVal);
      });
      return {
        smartObjectData: {
          ...state.smartObjectData,
          [smartObjectId]: updated
        },
        smartObjects: state.smartObjects.map((s) =>
          s.id === smartObjectId ? { ...s, recordsCount: updated.length } : s
        )
      };
    });

    try {
      const repo = getWorkflowRepository();
      await repo.deleteSmartObjectRecord(smartObjectId, primaryKeyVal);
    } catch (err) {
      console.warn('Repository deleteSmartObjectRecord error:', err);
    }
  },

  addUser: async (userData) => {
    const repo = getWorkflowRepository();
    const created = await repo.createUser(userData);
    set(state => ({
      users: [...state.users, created]
    }));
    return created;
  },

  deleteUser: async (id) => {
    const repo = getWorkflowRepository();
    await repo.deleteUser(id);
    set(state => ({
      users: state.users.filter(u => u.id !== id)
    }));
  },

  resetCanvas: () => {
    set({
      nodes: [],
      edges: [],
      selectedNodeId: null,
      workflowName: 'New Workflow',
      workflowDescription: ''
    });
  }
}));
