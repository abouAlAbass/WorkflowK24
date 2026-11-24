# Enterprise Nintex K2 Workflow Database Engine - Architectural Schema & Diagnostics

A production-ready, vendor-agnostic relational database architecture (ANSI SQL / SQL Server T-SQL) engineered for version control, runtime graph execution, task assignment SLAs, immutable audit trails, and node-level failure diagnostics.

---

## 1. Entity-Relationship Diagram (ERD)

```
+--------------------------+          +--------------------------+
|   WorkflowDefinitions    |          |     WorkflowVersions     |
+--------------------------+          +--------------------------+
| PK DefinitionID (INT)    |<--------1| PK VersionID (INT)       |
|    WorkflowKey (NVARCHAR)|          | FK DefinitionID (INT)    |
|    Name (NVARCHAR)       |          |    VersionNumber (INT)   |
|    Description           |          |    GraphJson (NVARCHAR)  |
|    Category              |          |    IsActive (BIT)        |
+--------------------------+          +--------------------------+
                                                   |
                                                   1
                                                   |
                                                   *
                                      +--------------------------+
                                      |    WorkflowInstances     |
                                      +--------------------------+
                                      | PK InstanceID (NVARCHAR) |
                                      | FK VersionID (INT)       |
                                      |    Status (NVARCHAR)     |
                                      |    CurrentNodeID         |
                                      |    CurrentNodeType       |
                                      |    InitiatorID           |
                                      |    ContextVariablesJson  |
                                      +--------------------------+
                                        /          |          \
                                       1           1           1
                                      /            |            \
                                     *             *             *
          +-----------------------+ +-------------------------+ +-----------------------+
          |    WorkflowTasks      | |  WorkflowInstanceErrors | |   WorkflowAuditLogs   |
          +-----------------------+ +-------------------------+ +-----------------------+
          | PK TaskID (NVARCHAR)  | | PK ErrorID (INT)        | | PK LogID (BIGINT)     |
          | FK InstanceID         | | FK InstanceID           | | FK InstanceID         |
          |    NodeID             | |    NodeID               | |    SequenceNumber   |
          |    AssigneeType       | |    NodeType             | |    NodeID           |
          |    AssigneeID         | |    ErrorCode            | |    EventType        |
          |    Status             | |    ErrorMessage         | |    EventSummary     |
          |    SelectedOutcome    | |    StackTrace           | |    OutcomeBranch    |
          |    DueDate            | |    InputSnapshotJson    | |    StateSnapshotJson|
          +-----------------------+ |    RetryCount           | +-----------------------+
                                    +-------------------------+
```

---

## 2. Table Specifications & Database Design Strategy

### A. Workflow Definitions & Version Control (`WorkflowDefinitions`, `WorkflowVersions`)
- **Immutability & Upgrades**: Workflow templates are keyed by a unique `WorkflowKey`. Graph structures (React Flow nodes, edges, position, and outcome handles) are stored as JSON in `WorkflowVersions.GraphJson`. Active running instances bind to a specific `VersionID` to prevent breaking changes when new workflow versions are published.

### B. Runtime Execution & Bottleneck Tracking (`WorkflowInstances`)
- **Active State**: Tracks running status (`RUNNING`, `COMPLETED`, `SUSPENDED`, `FAILED`, `CANCELLED`) and the current active node position (`CurrentNodeID`, `CurrentNodeType`).
- **Performance Index**: `IX_WorkflowInstances_Status_CurrentNode` enables instant lookup of active bottlenecks across thousands of execution instances.

### C. Task Assignments & Approvals (`WorkflowTasks`)
- **Multi-Assignee Support**: Assignee target can be `USER` (specific email) or `GROUP` (role queue e.g. `Finance Managers`).
- **Choice-Based Actioning**: Records the approver's selected decision outcome (`SelectedOutcome`), approval comments, SLA due date, and actual completed timestamp.

### D. Node-Level Failure Diagnostics (`WorkflowInstanceErrors`)
- **Exception Context**: Captures exact failing graph node ID (`NodeID`), category (`NodeType`), standardized error code, full exception stack trace, and stringified input payload snapshot (`InputSnapshotJson`).
- **Retry Mechanism**: Includes `RetryCount` and resolution status for automated or manual retry engine operations.

### E. Sequential Audit Timeline (`WorkflowAuditLogs`)
- **Immutable Tracing**: Captures every node entry (`NODE_ENTER`), node exit (`NODE_EXIT`), automated decision evaluation, and variable state mutation in strict chronological sequence (`SequenceNumber`).

---

## 3. Diagnostic SQL Queries

### Query 1: Active Bottlenecks Lookup
*Retrieves all running workflow instances currently blocked or waiting on user input at a specific node, including task SLA status.*

```sql
SELECT 
    wi.InstanceID,
    wd.Name AS WorkflowName,
    wv.VersionNumber,
    wi.CurrentNodeID,
    wi.CurrentNodeType,
    wi.InitiatorID,
    wt.TaskID,
    wt.TaskTitle,
    wt.AssigneeType,
    wt.AssigneeID,
    wt.AssignedAt,
    wt.DueDate,
    DATEDIFF(HOUR, wt.AssignedAt, SYSUTCDATETIME()) AS HoursWaiting,
    CASE 
        WHEN wt.DueDate IS NOT NULL AND SYSUTCDATETIME() > wt.DueDate THEN 1 
        ELSE 0 
    END AS IsSLABreached
FROM dbo.WorkflowInstances wi WITH (NOLOCK)
INNER JOIN dbo.WorkflowVersions wv ON wi.VersionID = wv.VersionID
INNER JOIN dbo.WorkflowDefinitions wd ON wv.DefinitionID = wd.DefinitionID
LEFT JOIN dbo.WorkflowTasks wt ON wi.InstanceID = wt.InstanceID AND wt.Status = 'PENDING'
WHERE wi.Status = 'RUNNING'
  AND wi.CurrentNodeID = 'node-approval-manager' -- Filter by specific blocked node
ORDER BY wt.AssignedAt ASC;
```

---

### Query 2: Node-Level Failure Diagnostics
*Extracts full exception diagnostic context for a failed workflow instance, including failing node ID, stack trace, error payload, and runtime variables.*

```sql
SELECT 
    wi.InstanceID,
    wd.Name AS WorkflowName,
    wi.Status AS InstanceStatus,
    we.ErrorID,
    we.NodeID AS FailingNodeID,
    we.NodeType AS FailingNodeType,
    we.ErrorCode,
    we.ErrorMessage,
    we.StackTrace,
    we.InputSnapshotJson AS RuntimeInputSnapshot,
    we.RetryCount,
    we.OccurredAt AS FailureTimestamp,
    wi.ContextVariablesJson AS ActiveVariablesAtFailure
FROM dbo.WorkflowInstances wi WITH (NOLOCK)
INNER JOIN dbo.WorkflowInstanceErrors we ON wi.InstanceID = we.InstanceID
INNER JOIN dbo.WorkflowVersions wv ON wi.VersionID = wv.VersionID
INNER JOIN dbo.WorkflowDefinitions wd ON wv.DefinitionID = wd.DefinitionID
WHERE wi.InstanceID = 'wf-inst-20260807-001' -- Target failed instance ID
ORDER BY we.OccurredAt DESC;
```

---

### Query 3: Reconstruct Chronological Execution Timeline
*Generates a complete step-by-step audit log of a workflow instance from trigger initiation to termination or failure.*

```sql
SELECT 
    wal.SequenceNumber,
    wal.LoggedAt,
    wal.EventType,
    wal.NodeID,
    wal.NodeType,
    wal.EventSummary,
    wal.OutcomeBranch AS DecisionOutcomeSelected,
    wal.StateSnapshotJson AS VariableDelta
FROM dbo.WorkflowAuditLogs wal WITH (NOLOCK)
WHERE wal.InstanceID = 'wf-inst-20260807-001'
ORDER BY wal.SequenceNumber ASC;
```
