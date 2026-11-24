-- ============================================================================
-- Enterprise Nintex K2 Workflow Engine Relational Database Schema
-- Syntax: ANSI SQL / SQL Server (T-SQL) Compliant
-- Version: 1.0.0
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. WORKFLOW DEFINITIONS & VERSION CONTROL
-- ----------------------------------------------------------------------------

-- Master Workflow Template Definition Registry
CREATE TABLE dbo.WorkflowDefinitions (
    DefinitionID        INT IDENTITY(1,1) NOT NULL,
    WorkflowKey         NVARCHAR(100) NOT NULL,
    Name                NVARCHAR(255) NOT NULL,
    Description         NVARCHAR(1000) NULL,
    Category            NVARCHAR(100) NOT NULL CONSTRAINT DF_WFDef_Category DEFAULT ('General'),
    CreatedBy           NVARCHAR(255) NOT NULL,
    CreatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_WFDef_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_WFDef_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    
    CONSTRAINT PK_WorkflowDefinitions PRIMARY KEY CLUSTERED (DefinitionID),
    CONSTRAINT UQ_WorkflowDefinitions_Key UNIQUE (WorkflowKey)
);

-- Versioned Workflow Definition Graph Snapshots
CREATE TABLE dbo.WorkflowVersions (
    VersionID           INT IDENTITY(1,1) NOT NULL,
    DefinitionID        INT NOT NULL,
    VersionNumber       INT NOT NULL,
    GraphJson           NVARCHAR(MAX) NOT NULL, -- Full React Flow graph JSON (nodes, edges, custom payload)
    Changelog           NVARCHAR(1000) NULL,
    IsActive            BIT NOT NULL CONSTRAINT DF_WFVer_IsActive DEFAULT (0),
    PublishedBy         NVARCHAR(255) NOT NULL,
    PublishedAt         DATETIME2(7) NOT NULL CONSTRAINT DF_WFVer_PublishedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_WorkflowVersions PRIMARY KEY CLUSTERED (VersionID),
    CONSTRAINT FK_WorkflowVersions_Definitions FOREIGN KEY (DefinitionID) 
        REFERENCES dbo.WorkflowDefinitions (DefinitionID) ON DELETE CASCADE,
    CONSTRAINT UQ_WorkflowVersions_Def_Ver UNIQUE (DefinitionID, VersionNumber)
);

-- ----------------------------------------------------------------------------
-- 2. RUNTIME INSTANCES & ACTIVE STATE MANAGEMENT
-- ----------------------------------------------------------------------------

-- Active & Historical Workflow Execution Instances
CREATE TABLE dbo.WorkflowInstances (
    InstanceID          NVARCHAR(50) NOT NULL, -- Unique GUID string (e.g., 'wf-inst-20260807-001')
    VersionID           INT NOT NULL,
    WorkflowKey         NVARCHAR(100) NOT NULL,
    Status              NVARCHAR(30) NOT NULL, -- 'RUNNING', 'COMPLETED', 'SUSPENDED', 'FAILED', 'CANCELLED'
    CurrentNodeID       NVARCHAR(100) NULL,    -- Current active node ID in the graph
    CurrentNodeType     NVARCHAR(50) NULL,     -- 'trigger', 'approval', 'sendEmail', 'decision', 'end'
    InitiatorID         NVARCHAR(255) NOT NULL, -- User email or System Service ID
    ContextVariablesJson NVARCHAR(MAX) NULL,   -- Active runtime variable state payload
    StartedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_WFInst_StartedAt DEFAULT (SYSUTCDATETIME()),
    CompletedAt         DATETIME2(7) NULL,
    LastUpdatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_WFInst_LastUpdatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_WorkflowInstances PRIMARY KEY CLUSTERED (InstanceID),
    CONSTRAINT FK_WorkflowInstances_Versions FOREIGN KEY (VersionID) 
        REFERENCES dbo.WorkflowVersions (VersionID),
    CONSTRAINT CK_WorkflowInstances_Status CHECK (Status IN ('RUNNING', 'COMPLETED', 'SUSPENDED', 'FAILED', 'CANCELLED'))
);

-- ----------------------------------------------------------------------------
-- 3. TASK & APPROVAL ASSIGNMENTS
-- ----------------------------------------------------------------------------

-- User and Group Task Assignments & Approval Decisions
CREATE TABLE dbo.WorkflowTasks (
    TaskID              NVARCHAR(50) NOT NULL, -- Unique Task GUID
    InstanceID          NVARCHAR(50) NOT NULL,
    NodeID              NVARCHAR(100) NOT NULL, -- Node ID from React Flow graph
    TaskTitle           NVARCHAR(255) NOT NULL,
    AssigneeType        NVARCHAR(20) NOT NULL, -- 'USER' or 'GROUP'
    AssigneeID          NVARCHAR(255) NOT NULL, -- Target user email or group name (e.g., 'Finance Managers')
    Status              NVARCHAR(30) NOT NULL, -- 'PENDING', 'APPROVED', 'REJECTED', 'DELEGATED', 'EXPIRED'
    SelectedOutcome     NVARCHAR(100) NULL,    -- Selected decision outcome choice (e.g., 'Approve', 'Reject', 'Request Info')
    Comments            NVARCHAR(MAX) NULL,    -- Approver decision comments
    DueDate             DATETIME2(7) NULL,     -- Task SLA deadline
    AssignedAt          DATETIME2(7) NOT NULL CONSTRAINT DF_WFTask_AssignedAt DEFAULT (SYSUTCDATETIME()),
    CompletedAt         DATETIME2(7) NULL,
    CompletedBy         NVARCHAR(255) NULL,    -- Actual user email who completed/delegated task

    CONSTRAINT PK_WorkflowTasks PRIMARY KEY CLUSTERED (TaskID),
    CONSTRAINT FK_WorkflowTasks_Instances FOREIGN KEY (InstanceID) 
        REFERENCES dbo.WorkflowInstances (InstanceID) ON DELETE CASCADE,
    CONSTRAINT CK_WorkflowTasks_AssigneeType CHECK (AssigneeType IN ('USER', 'GROUP')),
    CONSTRAINT CK_WorkflowTasks_Status CHECK (Status IN ('PENDING', 'APPROVED', 'REJECTED', 'DELEGATED', 'EXPIRED'))
);

-- ----------------------------------------------------------------------------
-- 4. NODE-LEVEL FAILURE DIAGNOSTICS & ERROR TRACKING
-- ----------------------------------------------------------------------------

-- Detailed Failure Log for Debugging and Exception Analysis
CREATE TABLE dbo.WorkflowInstanceErrors (
    ErrorID             INT IDENTITY(1,1) NOT NULL,
    InstanceID          NVARCHAR(50) NOT NULL,
    NodeID              NVARCHAR(100) NOT NULL, -- Exact failing graph node ID
    NodeType            NVARCHAR(50) NOT NULL,  -- Node category ('approval', 'sendEmail', 'decision', etc.)
    ErrorCode           NVARCHAR(100) NOT NULL, -- Standardized error classification code
    ErrorMessage        NVARCHAR(MAX) NOT NULL, -- Detailed human-readable error description
    StackTrace          NVARCHAR(MAX) NULL,     -- Full system exception stack trace
    InputSnapshotJson   NVARCHAR(MAX) NULL,     -- Exact stringified runtime input context at failure point
    RetryCount          INT NOT NULL CONSTRAINT DF_WFErr_RetryCount DEFAULT (0),
    IsResolved          BIT NOT NULL CONSTRAINT DF_WFErr_IsResolved DEFAULT (0),
    OccurredAt          DATETIME2(7) NOT NULL CONSTRAINT DF_WFErr_OccurredAt DEFAULT (SYSUTCDATETIME()),
    ResolvedAt          DATETIME2(7) NULL,

    CONSTRAINT PK_WorkflowInstanceErrors PRIMARY KEY CLUSTERED (ErrorID),
    CONSTRAINT FK_WorkflowInstanceErrors_Instances FOREIGN KEY (InstanceID) 
        REFERENCES dbo.WorkflowInstances (InstanceID) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- 5. IMMUTABLE EXECUTION AUDIT LOG & TIMELINE TRANSITIONS
-- ----------------------------------------------------------------------------

-- Sequential Event Log Capturing Node Transitions, Decisions, and Variable Updates
CREATE TABLE dbo.WorkflowAuditLogs (
    LogID               BIGINT IDENTITY(1,1) NOT NULL,
    InstanceID          NVARCHAR(50) NOT NULL,
    SequenceNumber      INT NOT NULL,           -- Monotonically increasing step counter per instance
    NodeID              NVARCHAR(100) NULL,     -- Affected graph node ID
    NodeType            NVARCHAR(50) NULL,
    EventType           NVARCHAR(50) NOT NULL,  -- 'NODE_ENTER', 'NODE_EXIT', 'DECISION_EVAL', 'TASK_ASSIGNED', 'VARIABLE_UPDATE', 'ERROR_RAISED'
    EventSummary        NVARCHAR(500) NOT NULL, -- Concise description of the execution step
    OutcomeBranch       NVARCHAR(100) NULL,    -- Outbound handle choice selected
    StateSnapshotJson   NVARCHAR(MAX) NULL,     -- Workflow variable state delta snapshot
    LoggedAt            DATETIME2(7) NOT NULL CONSTRAINT DF_WFAudit_LoggedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_WorkflowAuditLogs PRIMARY KEY CLUSTERED (LogID),
    CONSTRAINT FK_WorkflowAuditLogs_Instances FOREIGN KEY (InstanceID) 
        REFERENCES dbo.WorkflowInstances (InstanceID) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- STRATEGIC NON-CLUSTERED INDEXES FOR HIGH-PERFORMANCE QUERYING
-- ----------------------------------------------------------------------------

-- Index 1: Bottleneck Lookup - Fast retrieval of running instances blocked at a specific node
CREATE NONCLUSTERED INDEX IX_WorkflowInstances_Status_CurrentNode 
ON dbo.WorkflowInstances (Status, CurrentNodeID) 
INCLUDE (WorkflowKey, InitiatorID, StartedAt, LastUpdatedAt);

-- Index 2: Pending User & Group Tasks Lookup
CREATE NONCLUSTERED INDEX IX_WorkflowTasks_Assignee_Status 
ON dbo.WorkflowTasks (AssigneeID, Status, AssigneeType) 
INCLUDE (InstanceID, NodeID, TaskTitle, DueDate, AssignedAt);

-- Index 3: Instance Failure Diagnostics Lookup
CREATE NONCLUSTERED INDEX IX_WorkflowInstanceErrors_InstanceID 
ON dbo.WorkflowInstanceErrors (InstanceID, OccurredAt) 
INCLUDE (NodeID, NodeType, ErrorCode, RetryCount, IsResolved);

-- Index 4: Chronological Audit Timeline Reconstruction
CREATE NONCLUSTERED INDEX IX_WorkflowAuditLogs_Instance_Seq 
ON dbo.WorkflowAuditLogs (InstanceID, SequenceNumber ASC) 
INCLUDE (NodeID, EventType, EventSummary, OutcomeBranch, LoggedAt);
