-- ============================================================================
-- Enterprise Workflow Engine Database Schema: PostgreSQL
-- ============================================================================

CREATE TABLE IF NOT EXISTS "WorkflowDefinitions" (
    "DefinitionId"        SERIAL PRIMARY KEY,
    "WorkflowKey"         VARCHAR(100) NOT NULL UNIQUE,
    "Name"                VARCHAR(255) NOT NULL,
    "Description"         VARCHAR(1000) NULL,
    "Category"            VARCHAR(100) NOT NULL DEFAULT 'General',
    "CreatedBy"           VARCHAR(255) NOT NULL,
    "CreatedAt"           TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt"           TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS "WorkflowVersions" (
    "VersionId"           SERIAL PRIMARY KEY,
    "DefinitionId"        INT NOT NULL REFERENCES "WorkflowDefinitions"("DefinitionId") ON DELETE CASCADE,
    "VersionNumber"       INT NOT NULL,
    "GraphJson"           TEXT NOT NULL,
    "Changelog"           VARCHAR(1000) NULL,
    "IsActive"            BOOLEAN NOT NULL DEFAULT FALSE,
    "PublishedBy"         VARCHAR(255) NOT NULL,
    "PublishedAt"         TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "UQ_WorkflowVersions_Def_Ver" UNIQUE ("DefinitionId", "VersionNumber")
);

CREATE TABLE IF NOT EXISTS "WorkflowInstances" (
    "InstanceId"          VARCHAR(50) PRIMARY KEY,
    "VersionId"           INT NOT NULL REFERENCES "WorkflowVersions"("VersionId"),
    "WorkflowKey"         VARCHAR(100) NOT NULL,
    "Status"              VARCHAR(30) NOT NULL CHECK ("Status" IN ('Running', 'Completed', 'Suspended', 'Failed', 'Cancelled')),
    "CurrentNodeId"       VARCHAR(100) NULL,
    "CurrentNodeType"     VARCHAR(50) NULL,
    "InitiatorId"         VARCHAR(255) NOT NULL,
    "ContextVariablesJson" TEXT NULL,
    "StartedAt"           TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CompletedAt"         TIMESTAMPTZ NULL,
    "LastUpdatedAt"       TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS "WorkflowTasks" (
    "TaskId"              VARCHAR(50) PRIMARY KEY,
    "InstanceId"          VARCHAR(50) NOT NULL REFERENCES "WorkflowInstances"("InstanceId") ON DELETE CASCADE,
    "NodeId"              VARCHAR(100) NOT NULL,
    "TaskTitle"           VARCHAR(255) NOT NULL,
    "AssigneeType"        VARCHAR(20) NOT NULL CHECK ("AssigneeType" IN ('User', 'Group')),
    "AssigneeId"          VARCHAR(255) NOT NULL,
    "Status"              VARCHAR(30) NOT NULL CHECK ("Status" IN ('Pending', 'Approved', 'Rejected', 'Delegated', 'Expired')),
    "SelectedOutcome"     VARCHAR(100) NULL,
    "Comments"            TEXT NULL,
    "DueDate"             TIMESTAMPTZ NULL,
    "AssignedAt"          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CompletedAt"         TIMESTAMPTZ NULL,
    "CompletedBy"         VARCHAR(255) NULL
);

CREATE TABLE IF NOT EXISTS "WorkflowInstanceErrors" (
    "ErrorId"             SERIAL PRIMARY KEY,
    "InstanceId"          VARCHAR(50) NOT NULL REFERENCES "WorkflowInstances"("InstanceId") ON DELETE CASCADE,
    "NodeId"              VARCHAR(100) NOT NULL,
    "NodeType"            VARCHAR(50) NOT NULL,
    "ErrorCode"           VARCHAR(100) NOT NULL,
    "ErrorMessage"        TEXT NOT NULL,
    "StackTrace"          TEXT NULL,
    "InputSnapshotJson"   TEXT NULL,
    "RetryCount"          INT NOT NULL DEFAULT 0,
    "IsResolved"          BOOLEAN NOT NULL DEFAULT FALSE,
    "OccurredAt"          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "ResolvedAt"          TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS "WorkflowAuditLogs" (
    "LogId"               BIGSERIAL PRIMARY KEY,
    "InstanceId"          VARCHAR(50) NOT NULL REFERENCES "WorkflowInstances"("InstanceId") ON DELETE CASCADE,
    "SequenceNumber"      INT NOT NULL,
    "NodeId"              VARCHAR(100) NULL,
    "NodeType"            VARCHAR(50) NULL,
    "EventType"           VARCHAR(50) NOT NULL,
    "EventSummary"        VARCHAR(500) NOT NULL,
    "OutcomeBranch"       VARCHAR(100) NULL,
    "StateSnapshotJson"   TEXT NULL,
    "LoggedAt"            TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_WorkflowInstances_Status_CurrentNode"
ON "WorkflowInstances" ("Status", "CurrentNodeId");

CREATE INDEX IF NOT EXISTS "IX_WorkflowTasks_Assignee_Status"
ON "WorkflowTasks" ("AssigneeId", "Status", "AssigneeType");

CREATE INDEX IF NOT EXISTS "IX_WorkflowInstanceErrors_InstanceID"
ON "WorkflowInstanceErrors" ("InstanceId", "OccurredAt");

CREATE INDEX IF NOT EXISTS "IX_WorkflowAuditLogs_Instance_Seq"
ON "WorkflowAuditLogs" ("InstanceId", "SequenceNumber" ASC);
