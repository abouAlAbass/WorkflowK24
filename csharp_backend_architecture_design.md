# Architectural Design Specification: C# .NET 10 BPM Workflow Backend Engine

**System Architecture & High-Performance Execution Engine for Nintex K2 Visual Workflow Designer**

---

## 1. Clean Architecture & Layer Responsibilities

```
+-----------------------------------------------------------------------+
| Presentation Layer: ASP.NET Core Minimal APIs / FastEndpoints         |
| - REST API Endpoints, OpenAPI / Swagger Specs, DTO Request/Response  |
+-----------------------------------------------------------------------+
                                   |
                                   v
+-----------------------------------------------------------------------+
| Application Layer: CQRS Pattern via Wolverine                         |
| - Command Handlers, Query Handlers, Validation, Event Dispatcher      |
| - Graph Execution Engine (`IWorkflowExecutionEngine`)                 |
+-----------------------------------------------------------------------+
                                   |
                                   v
+-----------------------------------------------------------------------+
| Domain Layer: Core DDD Domain Model                                   |
| - Aggregates & Entities: WorkflowDefinition, WorkflowInstance, Task   |
| - Value Objects: ReactFlowGraph, Node, Edge, OutcomeHandle            |
| - Enums & Domain Events: WorkflowStartedEvent, TaskCompletedEvent     |
+-----------------------------------------------------------------------+
                                   ^
                                   |
+-----------------------------------------------------------------------+
| Infrastructure Layer: Persistence & External Integrations             |
| - EF Core `WorkflowEngineDbContext`, SQL Server / PostgreSQL Mappings |
| - System.Text.Json Value Converters, Outbox Transactional Messaging   |
+-----------------------------------------------------------------------+
```

### Layer Details

#### A. Domain Layer (DDD Core)
- **Aggregates & Entities**:
  - `WorkflowDefinition` (Root Aggregate): Encapsulates workflow keys, version collections, active version pointers.
  - `WorkflowVersion`: Holds version numbers, React Flow graph JSON snapshots, active flags.
  - `WorkflowInstance` (Root Aggregate): Manages runtime execution status (`Running`, `Completed`, `Suspended`, `Failed`, `Cancelled`), active node pointers, initiator context, and runtime variable dictionaries.
  - `WorkflowTask`: Represents approval/user decisions assigned to `User` or `Group`, selected decision outcomes (`Approve`, `Reject`, `Request Info`), due date SLAs, and approver comments.
  - `WorkflowError`: Node-level exception diagnostics storing failing `NodeID`, `NodeType`, standardized error code, stack trace, and runtime input snapshot.
  - `WorkflowAuditLog`: Monotonically ordered audit trail capturing `NodeEnter`, `NodeExit`, automated decisions, and variable state deltas.
- **Enums**: `WorkflowStatus`, `TaskStatus`, `AssigneeType`, `AuditEventType`.

#### B. Application Layer (CQRS & Execution Logic)
- **Wolverine CQRS Framework**: Leverages Wolverine for low-overhead, compile-time command/query handler dispatching and outbox transactional messaging.
- **Execution Navigation Engine (`IWorkflowExecutionEngine`)**: Stateful graph traversal engine that parses React Flow `nodes` and `edges`, matches `sourceHandle` outcome choices, evaluates conditional logic rules, and advances instance execution to downstream target nodes.

#### C. Infrastructure Layer (EF Core & Data Access)
- **EF Core 10 DbContext**: `WorkflowEngineDbContext` mapped to relational database tables using Fluent API configurations.
- **Native JSON Column Mapping**: Maps React Flow graph JSON (`GraphJson`), runtime variables (`ContextVariablesJson`), and error context (`InputSnapshotJson`) using `System.Text.Json` features and EF Core JSON value converters.

#### D. Presentation Layer (ASP.NET Core Minimal APIs)
- Lightweight, high-throughput Minimal API endpoints exposing REST contracts matching the React Flow visual designer.

---

## 2. Core Execution Engine Graph Navigation Algorithm

### Graph Traversal State Machine Logic

```
[Start Trigger / Previous Step]
              |
              v
  Parse React Flow Graph Payload
   (Extract Nodes & Edges Lists)
              |
              v
   Identify Outgoing Edges
  WHERE Edge.Source == CurrentNodeID
              |
              +-----------------------------------+
              |                                   |
    [Approval Task Node]                 [Decision Rule Node]
              |                                   |
   User Selects Decision               Evaluate If-Condition Rule
  (e.g., "Approve" / "Reject")        (e.g., Amount > $5000)
              |                                   |
   Match Edge.SourceHandle             Match Target Handle
  WHERE SourceHandle == Outcome       ("decision-true" / "decision-false")
              |                                   |
              +-----------------+-----------------+
                                |
                                v
                    Resolve Target Node ID
                   (Edge.Target == NextNodeID)
                                |
                                v
                   Execute Target Node Logic
                 (Create Task / Send Email / End)
```

### Transition Steps Description
1. **Graph Parsing**: The engine loads the active `WorkflowVersion.GraphJson` and deserializes it into strongly-typed `ReactFlowGraph` value objects.
2. **Edge Filtering**: Queries all edges where `edge.Source == currentNodeId`.
3. **Handle Choice Matching**:
   - For **Approval Task Nodes**: Matches the user's selected outcome choice against `edge.SourceHandle` or `edge.Label`.
   - For **Decision / Rule Nodes**: Evaluates the logical expression (`Field`, `Operator`, `Value`) against active runtime variables to select `decision-[#059669]` (True) or `decision-[#DC2626]` (False) handles.
4. **Target Node Resolution**: Resolves the downstream target node ID (`edge.Target`).
5. **Execution Dispatch**: Recursively executes downstream node logic:
   - If **Approval Node**: Generates a pending `WorkflowTask` record and awaits user input.
   - If **Send Email Node**: Dispatches automated email notification and auto-advances to next step.
   - If **End Node**: Marks `WorkflowInstance.Status = Completed` and logs final completion audit event.

---

## 3. Wolverine CQRS Commands, Queries, and Handlers

### Command & Query Contracts

| Type | Name | Purpose | Key Parameters |
| :--- | :--- | :--- | :--- |
| **Command** | `CreateWorkflowDefinitionCommand` | Save & publish React Flow graph JSON | `WorkflowKey`, `Name`, `GraphJson`, `CreatedBy`, `Category` |
| **Command** | `StartWorkflowInstanceCommand` | Instantiate workflow execution | `WorkflowKey`, `InitiatorId`, `TriggerVariables` |
| **Command** | `CompleteTaskCommand` | Process user approval decision | `TaskId`, `Outcome`, `CompletedBy`, `Comments` |
| **Query** | `GetActiveBottlenecksQuery` | Retrieve blocked/waiting instances | `NodeId` (optional filter) |
| **Query** | `GetFailingWorkflowQuery` | Extract node-level failure log | `InstanceId` |

### Detailed Command Execution Flow

#### A. `StartWorkflowInstanceCommand` Execution Flow
1. Fetch active `WorkflowVersion` for the given `WorkflowKey`.
2. Instantiate `WorkflowInstance` aggregate and record initial `WorkflowStarted` domain event.
3. Deserialize `GraphJson` and locate the `trigger` node.
4. Advance to downstream node using `EvaluateNextNode` algorithm.
5. Persist instance state and outbox events transactionally.

#### B. `CompleteTaskCommand` Execution Flow
1. Load `WorkflowTask` by `TaskId` with attached `WorkflowInstance` and `WorkflowVersion`.
2. Validate task status is `Pending`.
3. Call `task.Complete(Outcome, CompletedBy, Comments)` to update task state.
4. Invoke `EvaluateNextNode(graph, task.NodeId, outcome, variables)` to find downstream target handle node.
5. Transition instance to `nextNodeId` and execute downstream node logic.
6. Record sequential step log in `WorkflowAuditLogs`.
7. Commit Unit of Work within a single database transaction.

---

## 4. Entity Framework Core 10 Mapping & JSON Strategy

### Database Entity Mapping Matrix

- **`WorkflowDefinitions`**: Keyed by `DefinitionID` (Primary Key). Unique constraint on `WorkflowKey`. Has many `WorkflowVersions`.
- **`WorkflowVersions`**: Keyed by `VersionID`. Foreign Key to `DefinitionID`. Stores `GraphJson` as `NVARCHAR(MAX)` / `TEXT`. Unique constraint on `(DefinitionID, VersionNumber)`.
- **`WorkflowInstances`**: Keyed by `InstanceID` (`NVARCHAR(50)` GUID). Foreign Key to `VersionID`. Stores `ContextVariablesJson` as JSON column.
- **`WorkflowTasks`**: Keyed by `TaskID`. Foreign Key to `InstanceID`. Maps `AssigneeType` (`User`/`Group`) and `TaskStatus` as string/integer enums.
- **`WorkflowInstanceErrors`**: Keyed by `ErrorID`. Foreign Key to `InstanceID`. Stores `InputSnapshotJson` and stack traces.
- **`WorkflowAuditLogs`**: Keyed by `LogID` (`BIGINT`). Foreign Key to `InstanceID`. Ordered by `SequenceNumber`.

### Strategic Non-Clustered Indexes
1. `IX_WorkflowInstances_Status_CurrentNode`: Filtered index on `(Status, CurrentNodeID)` for instant bottleneck lookups.
2. `IX_WorkflowTasks_Assignee_Status`: Index on `(AssigneeID, Status, AssigneeType)` for user task list queries.
3. `IX_WorkflowInstanceErrors_InstanceID`: Index on `(InstanceID, OccurredAt)` for fast failure diagnostics extraction.
4. `IX_WorkflowAuditLogs_Instance_Seq`: Ordered index on `(InstanceID, SequenceNumber)` for timeline reconstruction.

---

## 5. API Presentation Layer Specifications & Contracts

### RESTful Minimal API Endpoint Specification

#### 1. POST `/api/workflows/definitions`
*Saves and publishes a versioned React Flow workflow definition graph.*
- **Request Body**:
  ```json
  {
    "workflowKey": "employee-onboarding",
    "name": "Employee Onboarding Approval Workflow",
    "category": "HR",
    "createdBy": "admin@company.com",
    "graphJson": "{\"nodes\":[...],\"edges\":[...]}"
  }
  ```
- **Response (201 Created)**:
  ```json
  {
    "definitionId": 1,
    "versionId": 1,
    "workflowKey": "employee-onboarding",
    "versionNumber": 1
  }
  ```

#### 2. POST `/api/workflows/instances/start`
*Instantiates a workflow execution instance from an active definition key.*
- **Request Body**:
  ```json
  {
    "workflowKey": "employee-onboarding",
    "initiatorId": "hr-recruiter@company.com",
    "triggerVariables": {
      "EmployeeName": "Alex Morgan",
      "Department": "Engineering",
      "Salary": 120000
    }
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "instanceId": "wf-inst-202608071815-a1b2",
    "status": "Running",
    "currentNodeId": "node-approval-manager",
    "startedAt": "2026-08-07T18:15:00Z"
  }
  ```

#### 3. POST `/api/workflows/tasks/complete`
*Processes a user approval decision ("Approve", "Reject", "Request Info") and executes downstream node graph transitions.*
- **Request Body**:
  ```json
  {
    "taskId": "task-c78b40ef",
    "outcome": "Approve",
    "completedBy": "manager@company.com",
    "comments": "Approved for hire."
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "taskId": "task-c78b40ef",
    "instanceId": "wf-inst-202608071815-a1b2",
    "outcome": "Approve",
    "nextNodeId": "node-email-welcome",
    "instanceStatus": "Running"
  }
  ```

#### 4. GET `/api/workflows/diagnostics/failures/{instanceId}`
*Retrieves node-level failure diagnostics, error message, stack trace, and input context snapshot.*
- **Response (200 OK)**:
  ```json
  {
    "instanceId": "wf-inst-202608071815-a1b2",
    "workflowKey": "employee-onboarding",
    "failingNodeId": "node-email-welcome",
    "failingNodeType": "sendEmail",
    "errorCode": "SMTP_CONNECT_FAILED",
    "errorMessage": "Unable to connect to SMTP gateway at smtp.company.com:587",
    "stackTrace": "System.Net.Sockets.SocketException: Connection refused...",
    "inputSnapshotJson": "{\"to\":\"alex@company.com\",\"subject\":\"Welcome\"}",
    "occurredAt": "2026-08-07T18:15:30Z"
  }
  ```

#### 5. GET `/api/workflows/diagnostics/bottlenecks?nodeId={nodeId}`
*Retrieves active running workflow instances currently blocked or waiting at specific task nodes.*
- **Response (200 OK)**:
  ```json
  [
    {
      "instanceId": "wf-inst-202608071815-a1b2",
      "workflowKey": "employee-onboarding",
      "currentNodeId": "node-approval-manager",
      "currentNodeType": "approval",
      "initiatorId": "hr-recruiter@company.com",
      "startedAt": "2026-08-07T18:15:00Z",
      "pendingTaskId": "task-c78b40ef",
      "taskTitle": "Department Head Approval",
      "assigneeId": "Engineering Managers",
      "taskAssignedAt": "2026-08-07T18:15:01Z"
    }
  ]
  ```
