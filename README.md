# Enterprise Workflow Engine (BPM Visual Designer & Execution Engine)

An enterprise-grade Business Process Management (BPM) Visual Workflow Designer and high-throughput execution engine inspired by Nintex K2. Built with **React 19 / TypeScript / Vite 8** on the frontend and **.NET 10 / ASP.NET Core Minimal APIs / Wolverine CQRS / EF Core** on the backend.

---

## Architecture Overview

The system strictly follows **Clean Architecture & Hexagonal Architecture (Ports & Adapters)** principles. Dependencies point inward only:

```
[ Frontend: React 19 / @xyflow/react ]
                   │
                   ▼ (HTTP / JWT)
+─────────────────────────────────────────────────────────────+
| WorkflowEngine.Api (Presentation Adapter)                   |
| - ASP.NET Core Minimal APIs, OpenAPI / Scalar Documentation |
| - Wolverine Host, JWT Bearer Auth, CORS, Health Checks      |
+─────────────────────────────────────────────────────────────+
                               │
                               ▼
+─────────────────────────────────────────────────────────────+
| WorkflowEngine.Application (Application Core & Ports)       |
| - Wolverine CQRS Command & Query Handlers                   |
| - FluentValidation Command Validators                       |
| - Graph Execution Engine (`IWorkflowExecutionEngine`)       |
| - Ports: `IWorkflow*Repository`, `IUnitOfWork`, `IEmail...` |
+─────────────────────────────────────────────────────────────+
                               │
                               ▼
+─────────────────────────────────────────────────────────────+
| WorkflowEngine.Domain (Domain DDD Core - Zero Dependencies) |
| - Aggregates: WorkflowDefinition, WorkflowInstance          |
| - Entities: WorkflowVersion, WorkflowTask, WorkflowError    |
| - Value Objects: ReactFlowGraph, Nodes, Edges, Enums        |
+─────────────────────────────────────────────────────────────+
                               ▲
                               │
+─────────────────────────────────────────────────────────────+
| WorkflowEngine.Infrastructure (Persistence Adapter)         |
| - EF Core 10 `WorkflowEngineDbContext` mapped to schema.sql |
| - Dual Provider: PostgreSQL (Npgsql) & SQL Server           |
| - Repository Adapters & Unit of Work Implementation         |
+─────────────────────────────────────────────────────────────+
```

---

## Operating Modes

The frontend supports two distinct operating modes, chosen upon startup or toggled via the toolbar badge:

### 1. Demo Mode (In-Memory, No Backend Required)
- **Zero backend or database setup required.**
- All workflow simulations, approval tasks, and sample instances run purely client-side via `InMemoryWorkflowRepository`.
- Seeded with realistic enterprise HR, Finance, and IT Security workflows.
- Workflow definitions and edits persist to browser `localStorage`.

### 2. Connected Mode (Live .NET 10 API)
- Connects live to the ASP.NET Core API via `HttpWorkflowRepository`.
- Secure JWT Bearer authentication with pre-configured personas (`Sarah Jenkins - Admin`, `Michael Chen - Finance`).
- Real database persistence in SQL Server or PostgreSQL.
- Server-side graph navigation, automated decision branching, and task assignment through Wolverine CQRS.

---

## Quick Start Guide

### Frontend Setup

1. **Install Dependencies** (if not already installed):
   ```bash
   npm install
   ```

2. **Configure Environment** (Optional):
   ```env
   # .env
   VITE_API_BASE_URL=http://localhost:5000/api
   ```

3. **Launch Frontend**:
   ```bash
   npm run dev
   ```
   Open `http://localhost:5173` in your browser. A modal will prompt you to choose **Demo Mode** or **Connected Mode**.

---

### Backend Setup (.NET 10)

The backend solution is located in `backend/WorkflowEngine.sln`.

1. **Run the API**:
   ```bash
   cd backend/src/WorkflowEngine.Api
   dotnet run
   ```

2. **Access Interactive API Documentation**:
   - Scalar UI: `http://localhost:5000/scalar/v1`
   - OpenAPI Specification: `http://localhost:5000/openapi/v1.json`
   - Health Check: `http://localhost:5000/health`

---

## Database Configuration & Multi-Provider Switching

The engine supports both **PostgreSQL** and **SQL Server**. The active provider is controlled by `appsettings.json`:

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=(localdb)\\mssqllocaldb;Database=WorkflowEngineDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### Switching to PostgreSQL
Change `Provider` to `PostgreSql` and supply the connection string:
```json
{
  "Database": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=workflow_engine;Username=postgres;Password=YourPassword;"
  }
}
```

*Note: The engine validates this setting on startup and will fail fast with a descriptive error if an unsupported provider is specified.*

### EF Core Migrations
The initial standard EF Core migration is generated in `backend/src/WorkflowEngine.Infrastructure/Migrations/`:
- `InitialCreate.cs`: Creates all tables, foreign keys, unique constraints, and indexes.

You can apply or add new migrations via the standard dotnet tool CLI:
```powershell
# Add a new migration
dotnet dotnet-ef migrations add <MigrationName> `
  --project backend\src\WorkflowEngine.Infrastructure `
  --startup-project backend\src\WorkflowEngine.Api

# Update database directly
dotnet dotnet-ef database update `
  --project backend\src\WorkflowEngine.Infrastructure `
  --startup-project backend\src\WorkflowEngine.Api
```

*Note: You can also simply run the backend API (`dotnet run`), which automatically detects and applies pending EF Core migrations on startup.*

### DDL Schema Scripts
Database scripts compliant with `schema.sql` are also available in:
- SQL Server: `backend/src/WorkflowEngine.Infrastructure/Persistence/Scripts/schema-sqlserver.sql`
- PostgreSQL: `backend/src/WorkflowEngine.Infrastructure/Persistence/Scripts/schema-postgresql.sql`

---

## Test Suites

The backend includes comprehensive test coverage:

| Test Project | Focus Area | Framework |
| :--- | :--- | :--- |
| `WorkflowEngine.Domain.UnitTests` | Entity invariants, versioning, transitions, error tracking | xUnit, FluentAssertions |
| `WorkflowEngine.Application.UnitTests` | Graph execution engine rule evaluation, decision branching, Wolverine handlers | xUnit, Moq, FluentAssertions |
| `WorkflowEngine.Infrastructure.IntegrationTests` | EF Core mapping verification, table constraints, multi-provider configuration | xUnit, EF Core InMemory / Testcontainers |
| `WorkflowEngine.Api.IntegrationTests` | Health check, JWT authentication, and endpoint contracts | xUnit, WebApplicationFactory |

---

## Documentation Links

- [docs/STATUS.md](file:///d:/workflow/docs/STATUS.md): Complete audit, gap analysis, and implementation checklist.
- [docs/DECISIONS.md](file:///d:/workflow/docs/DECISIONS.md): Architectural decisions and design trade-offs.
- [schema.sql](file:///d:/workflow/schema.sql): Canonical relational schema specification.
