# Project Status & Implementation Checklist (STATUS.md)

Last Updated: Step 5 Final Verification & Documentation

## 1. Repository Inventory & Folder Structure

### Backend Projects (.NET 10 Solution)
- `backend/WorkflowEngine.sln`
- `backend/Directory.Build.props`: Central build properties (`net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`)
- `backend/Directory.Packages.props`: Central package management (EF Core 10, Wolverine 3.9, FluentValidation, Testcontainers, xUnit)
- `backend/.editorconfig`: Code formatting and naming rules
- Source Projects (`backend/src/`):
  - `WorkflowEngine.Domain`: Pure domain aggregates, entities, enums, value objects, domain repository ports.
  - `WorkflowEngine.Application`: Wolverine CQRS commands, queries, DTOs, FluentValidation validators, handlers, graph execution engine port and implementation.
  - `WorkflowEngine.Infrastructure`: EF Core `WorkflowEngineDbContext`, multi-provider configuration (PostgreSQL & SQL Server), repository adapters, unit of work, DDL scripts.
  - `WorkflowEngine.Api`: ASP.NET Core Minimal APIs, feature endpoint groups, Wolverine host integration, JWT authentication, OpenAPI/Scalar, health checks, CORS.
- Test Projects (`backend/tests/`):
  - `WorkflowEngine.Domain.UnitTests`: Aggregate invariant, lifecycle, state transition, and error resolution tests.
  - `WorkflowEngine.Application.UnitTests`: Graph execution engine condition evaluation, decision branching, and Wolverine handler tests.
  - `WorkflowEngine.Infrastructure.IntegrationTests`: EF Core model mapping, table name and constraint verification, multi-provider configuration tests.
  - `WorkflowEngine.Api.IntegrationTests`: Minimal API integration tests (`/health`, `/api/auth/login`, OpenAPI contracts).

### Frontend (React 19, Vite 8, TypeScript, Tailwind 4)
- `src/services/workflowRepository.ts`: Repository port interface.
- `src/services/inMemoryWorkflowRepository.ts`: Demo mode adapter with seeded templates and zero backend requirement.
- `src/services/httpWorkflowRepository.ts`: Connected mode adapter communicating with ASP.NET Core API via JWT Bearer tokens.
- `src/services/repositoryManager.ts`: Dynamic repository provider and runtime mode switcher.
- `src/components/modals/ModeSelectorModal.tsx`: Startup and on-demand modal to switch between Demo and Connected mode.
- `.env` & `.env.example`: Configures `VITE_API_BASE_URL`.

---

## 2. File Classification by Target Layer

| File / Component Path | Target Layer | Target Project / Location | Status |
| :--- | :--- | :--- | :--- |
| `Domain/Enums/WorkflowEnums.cs` | **Domain** | `backend/src/WorkflowEngine.Domain/Enums/WorkflowEnums.cs` | **Done** |
| `Domain/Entities/*.cs` | **Domain** | `backend/src/WorkflowEngine.Domain/Entities/*.cs` | **Done** (Clean 1 file per entity) |
| `Domain/ValueObjects/ReactFlowGraph.cs` | **Domain** | `backend/src/WorkflowEngine.Domain/ValueObjects/ReactFlowGraph.cs` | **Done** |
| `Domain/Repositories/*.cs` | **Domain** | `backend/src/WorkflowEngine.Domain/Repositories/*.cs` | **Done** (Repository & Unit of Work ports) |
| `Application/Services/WorkflowExecutionEngine.cs` | **Application** | `backend/src/WorkflowEngine.Application/Services/WorkflowExecutionEngine.cs` | **Done** (Fixed label bug & error recording) |
| `Application/Features/**` | **Application** | `backend/src/WorkflowEngine.Application/Features/**` | **Done** (Clean CQRS features & handlers) |
| `Infrastructure/Persistence/**` | **Infrastructure** | `backend/src/WorkflowEngine.Infrastructure/Persistence/**` | **Done** (DbContext, Configurations, Repositories) |
| `Infrastructure/DependencyInjection.cs` | **Infrastructure** | `backend/src/WorkflowEngine.Infrastructure/DependencyInjection.cs` | **Done** (PostgreSQL & SQL Server multi-provider) |
| `Api/Program.cs` & `Api/Endpoints/**` | **Api** | `backend/src/WorkflowEngine.Api/**` | **Done** (Minimal APIs, Auth, Wolverine, Swagger) |
| `Frontend Services & Mode Selector` | **Frontend** | `src/services/**`, `src/components/modals/ModeSelectorModal.tsx` | **Done** (Dual Mode: Demo vs. Connected) |
| `Unit & Integration Tests` | **Tests** | `backend/tests/**` | **Done** (Domain, Application, Infrastructure, Api) |

---

## 3. Master Checklist

- [x] **Step 0: Audit and gap analysis**
  - [x] Repository inventory & baseline build assessment
  - [x] File classification by target layer
  - [x] Gap analysis (complete, incomplete, missing, violations)
  - [x] Schema comparison against `schema.sql`
  - [x] `docs/STATUS.md` created
- [x] **Step 1: Solution structure (.NET 10)**
  - [x] Create `WorkflowEngine.sln`
  - [x] Create `WorkflowEngine.Domain.csproj`
  - [x] Create `WorkflowEngine.Application.csproj`
  - [x] Create `WorkflowEngine.Infrastructure.csproj`
  - [x] Create `WorkflowEngine.Api.csproj`
  - [x] Create test projects (`WorkflowEngine.Domain.UnitTests`, `WorkflowEngine.Application.UnitTests`, `WorkflowEngine.Infrastructure.IntegrationTests`, `WorkflowEngine.Api.IntegrationTests`)
  - [x] Add `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, update `.gitignore`
  - [x] Migrate and organize files into clean one-type-per-file structure
  - [x] Fix namespaces to `WorkflowEngine.*`
  - [x] Enforce dependency rule: Domain has zero framework references; Application references Domain only; Infrastructure & Api depend inward
- [x] **Step 2: Infrastructure (EF Core, Multi-Provider)**
  - [x] Implement `WorkflowEngineDbContext`
  - [x] Implement `IEntityTypeConfiguration<T>` for all entities matching `schema.sql` (tables, keys, indexes, string conversions, cascades)
  - [x] Implement multi-provider configuration supporting `PostgreSql` and `SqlServer` via `appsettings.json` (`Database:Provider`)
  - [x] Define repository ports in Domain/Application and implement adapters in Infrastructure
  - [x] Implement `AddInfrastructure(IConfiguration)` extension method with fail-fast validation
  - [x] Provide SQL scripts for both PostgreSQL (`schema-postgresql.sql`) and SQL Server (`schema-sqlserver.sql`)
  - [x] Generate standard EF Core `InitialCreate` migration in `WorkflowEngine.Infrastructure/Migrations/`
- [x] **Step 3: API (Minimal APIs) & Wolverine**
  - [x] Organize endpoints into feature groups (`/api/definitions`, `/api/instances`, `/api/tasks`, `/api/diagnostics`, `/api/auth`)
  - [x] Configure Wolverine for CQRS command/query handling and messaging
  - [x] Add FluentValidation validators for all commands
  - [x] Add OpenAPI / Scalar documentation, ProblemDetails exception handling, Health Checks (`/health`), CORS, structured logging
  - [x] Add JWT authentication and login endpoint for Connected mode
- [x] **Step 4: Frontend (Demo or Connected Mode)**
  - [x] Extract data access behind a single `WorkflowRepository` interface (port)
  - [x] Implement `InMemoryWorkflowRepository` for Demo mode (seeded data, zero backend calls)
  - [x] Implement `HttpWorkflowRepository` for Connected mode (authenticates against live backend API)
  - [x] Create startup choice modal/screen: Demo mode vs. Connected mode (`ModeSelectorModal.tsx`)
  - [x] Provide `.env` and `.env.example` configuration for API base URL
- [x] **Step 5: Testing & Documentation**
  - [x] Domain & Application unit tests
  - [x] Infrastructure & API integration tests
  - [x] Complete `README.md` and `docs/DECISIONS.md`
