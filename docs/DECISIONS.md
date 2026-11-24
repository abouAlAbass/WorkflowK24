# Architecture & Implementation Decisions (DECISIONS.md)

This log records architectural decisions, assumptions, and alignments made during the implementation of the Workflow Engine.

---

## ADR-001: Target Framework and Solution Naming
- **Decision**: Standardize all backend projects under the root namespace `WorkflowEngine.*` using .NET 10 (`net10.0`), with strict C# nullable reference types (`<Nullable>enable</Nullable>`) and treat warnings as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
- **Rationale**: The previous code used `NintexK2.WorkflowEngine.*`, while the specification calls for clean architecture layer names (`WorkflowEngine.Domain`, `WorkflowEngine.Application`, `WorkflowEngine.Infrastructure`, `WorkflowEngine.Api`).
- **Date**: 2026-10-17

## ADR-002: Inversion of Control for Wolverine Handlers & Entity Framework Core
- **Decision**: Remove direct references to `WorkflowEngineDbContext` and `Microsoft.EntityFrameworkCore` from `WorkflowEngine.Application`. Instead, define repository interfaces (`IWorkflowDefinitionRepository`, `IWorkflowInstanceRepository`, `IWorkflowTaskRepository`) and `IUnitOfWork` inside `WorkflowEngine.Application` / `WorkflowEngine.Domain`.
- **Rationale**: In Clean Architecture, the Application layer must not depend on Infrastructure or specific ORM implementations. This enables testing Application handlers using pure in-memory mock repositories and prevents database leaking into business workflows.
- **Date**: 2026-10-17

## ADR-003: Resolution of Incomplete Entity Members (`WorkflowError`)
- **Decision**: Add a `Resolve()` method to `WorkflowError` entity that updates `IsResolved = true` and `ResolvedAt = DateTime.UtcNow`.
- **Rationale**: `schema.sql` defines `IsResolved BIT` and `ResolvedAt DATETIME2(7) NULL`. The entity had these private setters but lacked a domain method to resolve errors.
- **Date**: 2026-10-17

## ADR-004: Fix for `ApprovalDataPayload` and Node Label in Execution Engine
- **Decision**: In `WorkflowExecutionEngine.cs`, use `node.Data.Label ?? "Approval Task"` when creating tasks, rather than `approval.Label` (which does not exist on `ApprovalDataPayload`).
- **Rationale**: `ReactFlowNodeData` contains the `Label` property for the node. `ApprovalDataPayload` holds execution specifics (`assignmentType`, `assignee`, `outcomes`, etc.).
- **Date**: 2026-10-17

## ADR-005: Dual Database Provider Strategy (PostgreSQL & SQL Server)
- **Decision**: Configure `WorkflowEngineDbContext` using provider-agnostic Fluent API configurations, with provider-specific configuration extensions for PostgreSQL (`UseNpgsql`) and SQL Server (`UseSqlServer`). Selected via `Database:Provider` in `appsettings.json`.
- **Rationale**: Adheres to specification Step 2 requirements, allowing deployments on either cloud PostgreSQL or enterprise SQL Server instances with zero code changes.
- **Date**: 2026-10-17

## ADR-006: Frontend Repository Port & Dual Mode (Demo vs. Connected)
- **Decision**: Introduce a `WorkflowRepository` interface in `src/services/workflowRepository.ts`. Provide:
  1. `InMemoryWorkflowRepository` for Demo mode (using the existing seeded data, zero API calls).
  2. `HttpWorkflowRepository` for Connected mode (calling `/api/*` endpoints with JWT auth token).
  A startup modal allows users to pick their operating mode and log in if Connected mode is selected.
- **Rationale**: Fulfills Step 4 requirements while completely insulating UI components from backend communication specifics.
- **Date**: 2026-10-17

## ADR-007: Backend Smart Objects Entity Schema and Dynamic Records Store
- **Decision**: Add `SmartObjectEntity` and `SmartObjectRecordEntity` to `WorkflowEngine.Domain`, backed by EF Core configurations and `ISmartObjectRepository`. Provide full Minimal API endpoints under `/api/smartobjects` matching the frontend's `httpWorkflowRepository.ts` contract.
- **Rationale**: Enables dynamic enterprise data model definitions and row storage in PostgreSQL/SQL Server, keeping the frontend and backend 100% in sync for Connected mode.
- **Date**: 2026-10-17

## ADR-008: Stage Sub-Workflow Traversal in WorkflowExecutionEngine
- **Decision**: Extend `ReactFlowNodeData` with `StageDataPayload` (containing `SubWorkflowPayload`). When a `stage` node is traversed by `WorkflowExecutionEngine`, the engine creates a sub-graph frame, navigates the sub-workflow from its trigger, and then continues outward along the parent graph's outgoing edges.
- **Rationale**: Matches the frontend's visual composite Stage sub-process designer and allows recursive hierarchical workflow execution.
- **Date**: 2026-10-17

