-- ============================================================================
-- Enterprise Workflow Engine Database Schema: SQL Server (T-SQL)
-- ============================================================================

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

CREATE TABLE dbo.WorkflowVersions (
    VersionID           INT IDENTITY(1,1) NOT NULL,
    DefinitionID        INT NOT NULL,
    VersionNumber       INT NOT NULL,
    GraphJson           NVARCHAR(MAX) NOT NULL,
    Changelog           NVARCHAR(1000) NULL,
    IsActive            BIT NOT NULL CONSTRAINT DF_WFVer_IsActive DEFAULT (0),
    PublishedBy         NVARCHAR(255) NOT NULL,
    PublishedAt         DATETIME2(7) NOT NULL CONSTRAINT DF_WFVer_PublishedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_WorkflowVersions PRIMARY KEY CLUSTERED (VersionID),
    CONSTRAINT FK_WorkflowVersions_Definitions FOREIGN KEY (DefinitionID) 
        REFERENCES dbo.WorkflowDefinitions (DefinitionID) ON DELETE CASCADE,
    CONSTRAINT UQ_WorkflowVersions_Def_Ver UNIQUE (DefinitionID, VersionNumber)
);

CREATE TABLE dbo.WorkflowInstances (
    InstanceID          NVARCHAR(50) NOT NULL,
    VersionID           INT NOT NULL,
    WorkflowKey         NVARCHAR(100) NOT NULL,
    Status              NVARCHAR(30) NOT NULL,
    CurrentNodeID       NVARCHAR(100) NULL,
    CurrentNodeType     NVARCHAR(50) NULL,
    InitiatorID         NVARCHAR(255) NOT NULL,
    ContextVariablesJson NVARCHAR(MAX) NULL,
    StartedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_WFInst_StartedAt DEFAULT (SYSUTCDATETIME()),
    CompletedAt         DATETIME2(7) NULL,
    LastUpdatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_WFInst_LastUpdatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_WorkflowInstances PRIMARY KEY CLUSTERED (InstanceID),
    CONSTRAINT FK_WorkflowInstances_Versions FOREIGN KEY (VersionID) 
        REFERENCES dbo.WorkflowVersions (VersionID),
    CONSTRAINT CK_WorkflowInstances_Status CHECK (Status IN ('Running', 'Completed', 'Suspended', 'Failed', 'Cancelled'))
);

CREATE TABLE dbo.WorkflowTasks (
    TaskID              NVARCHAR(50) NOT NULL,
    InstanceID          NVARCHAR(50) NOT NULL,
    NodeID              NVARCHAR(100) NOT NULL,
    TaskTitle           NVARCHAR(255) NOT NULL,
    AssigneeType        NVARCHAR(20) NOT NULL,
    AssigneeID          NVARCHAR(255) NOT NULL,
    Status              NVARCHAR(30) NOT NULL,
    SelectedOutcome     NVARCHAR(100) NULL,
    Comments            NVARCHAR(MAX) NULL,
    DueDate             DATETIME2(7) NULL,
    AssignedAt          DATETIME2(7) NOT NULL CONSTRAINT DF_WFTask_AssignedAt DEFAULT (SYSUTCDATETIME()),
    CompletedAt         DATETIME2(7) NULL,
    CompletedBy         NVARCHAR(255) NULL,

    CONSTRAINT PK_WorkflowTasks PRIMARY KEY CLUSTERED (TaskID),
    CONSTRAINT FK_WorkflowTasks_Instances FOREIGN KEY (InstanceID) 
        REFERENCES dbo.WorkflowInstances (InstanceID) ON DELETE CASCADE,
    CONSTRAINT CK_WorkflowTasks_AssigneeType CHECK (AssigneeType IN ('User', 'Group')),
    CONSTRAINT CK_WorkflowTasks_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected', 'Delegated', 'Expired'))
);

CREATE TABLE dbo.WorkflowInstanceErrors (
    ErrorID             INT IDENTITY(1,1) NOT NULL,
    InstanceID          NVARCHAR(50) NOT NULL,
    NodeID              NVARCHAR(100) NOT NULL,
    NodeType            NVARCHAR(50) NOT NULL,
    ErrorCode           NVARCHAR(100) NOT NULL,
    ErrorMessage        NVARCHAR(MAX) NOT NULL,
    StackTrace          NVARCHAR(MAX) NULL,
    InputSnapshotJson   NVARCHAR(MAX) NULL,
    RetryCount          INT NOT NULL CONSTRAINT DF_WFErr_RetryCount DEFAULT (0),
    IsResolved          BIT NOT NULL CONSTRAINT DF_WFErr_IsResolved DEFAULT (0),
    OccurredAt          DATETIME2(7) NOT NULL CONSTRAINT DF_WFErr_OccurredAt DEFAULT (SYSUTCDATETIME()),
    ResolvedAt          DATETIME2(7) NULL,

    CONSTRAINT PK_WorkflowInstanceErrors PRIMARY KEY CLUSTERED (ErrorID),
    CONSTRAINT FK_WorkflowInstanceErrors_Instances FOREIGN KEY (InstanceID) 
        REFERENCES dbo.WorkflowInstances (InstanceID) ON DELETE CASCADE
);

CREATE TABLE dbo.WorkflowAuditLogs (
    LogID               BIGINT IDENTITY(1,1) NOT NULL,
    InstanceID          NVARCHAR(50) NOT NULL,
    SequenceNumber      INT NOT NULL,
    NodeID              NVARCHAR(100) NULL,
    NodeType            NVARCHAR(50) NULL,
    EventType           NVARCHAR(50) NOT NULL,
    EventSummary        NVARCHAR(500) NOT NULL,
    OutcomeBranch       NVARCHAR(100) NULL,
    StateSnapshotJson   NVARCHAR(MAX) NULL,
    LoggedAt            DATETIME2(7) NOT NULL CONSTRAINT DF_WFAudit_LoggedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_WorkflowAuditLogs PRIMARY KEY CLUSTERED (LogID),
    CONSTRAINT FK_WorkflowAuditLogs_Instances FOREIGN KEY (InstanceID) 
        REFERENCES dbo.WorkflowInstances (InstanceID) ON DELETE CASCADE
);

CREATE NONCLUSTERED INDEX IX_WorkflowInstances_Status_CurrentNode 
ON dbo.WorkflowInstances (Status, CurrentNodeID);

CREATE NONCLUSTERED INDEX IX_WorkflowTasks_Assignee_Status 
ON dbo.WorkflowTasks (AssigneeID, Status, AssigneeType);

CREATE NONCLUSTERED INDEX IX_WorkflowInstanceErrors_InstanceID 
ON dbo.WorkflowInstanceErrors (InstanceID, OccurredAt);

CREATE NONCLUSTERED INDEX IX_WorkflowAuditLogs_Instance_Seq 
ON dbo.WorkflowAuditLogs (InstanceID, SequenceNumber ASC);
