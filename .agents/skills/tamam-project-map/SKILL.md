---
name: tamam-project-map
description: >-
  Locate files across the Tamam Workflow Studio (.NET 10 + React 19) codebase.
  Trigger whenever you need exact paths for backend layers, endpoints, handlers,
  entities, DbContext, migrations, repositories, or frontend stores, services,
  types, and views.
---

# Tamam Project Map

## Solution & Projects (.NET 10)
- Solution: `backend/WorkflowEngine.sln`
- Domain: `backend/src/WorkflowEngine.Domain/`
- Application: `backend/src/WorkflowEngine.Application/`
- Infrastructure: `backend/src/WorkflowEngine.Infrastructure/`
- Api: `backend/src/WorkflowEngine.Api/`

## Backend Key Locations
- Endpoints: `backend/src/WorkflowEngine.Api/Endpoints/`
- Auth / JWT: `backend/src/WorkflowEngine.Api/Auth/`
- Handlers & Features (CQRS): `backend/src/WorkflowEngine.Application/Features/<Domain>/`
  - Domains: `Analytics`, `Definitions`, `Diagnostics`, `Instances`, `Settings`, `SmartForms`, `SmartObjects`, `Tasks`, `Users`
- Application Interfaces & Security: `backend/src/WorkflowEngine.Application/Common/{Interfaces,Security,Exceptions}/`
- Application Services: `backend/src/WorkflowEngine.Application/Services/`
- Entities & ValueObjects: `backend/src/WorkflowEngine.Domain/{Entities,Enums,ValueObjects,Security}/`
- Repository Interfaces: `backend/src/WorkflowEngine.Domain/Repositories/`
- DbContext & Seeder: `backend/src/WorkflowEngine.Infrastructure/Persistence/WorkflowEngineDbContext.cs`, `DatabaseInitializer.cs`
- EF Configurations: `backend/src/WorkflowEngine.Infrastructure/Persistence/Configurations/`
- Repository Implementations: `backend/src/WorkflowEngine.Infrastructure/Persistence/Repositories/`
- EF Core Migrations: `backend/src/WorkflowEngine.Infrastructure/Migrations/`

## Backend Tests
- API Integration: `backend/tests/WorkflowEngine.Api.IntegrationTests/Endpoints/`
- Application Unit: `backend/tests/WorkflowEngine.Application.UnitTests/{Features,Handlers,Services}/`
- Domain Unit: `backend/tests/WorkflowEngine.Domain.UnitTests/Entities/`
- Infrastructure Integration: `backend/tests/WorkflowEngine.Infrastructure.IntegrationTests/Persistence/`

## Frontend Key Locations (React 19 + Vite)
- Types: `src/types/`
- Zustand Stores: `src/store/useWorkflowStore.ts`, `src/store/useSmartFormStore.ts`
- Services / Repositories: `src/services/` (`workflowRepository.ts`, `httpWorkflowRepository.ts`, `inMemoryWorkflowRepository.ts`, `repositoryManager.ts`)
- Views: `src/components/views/`
- UI Components: `src/components/{canvas,dashboard,forms,header,layout,modals,nodes,palette,sidebar}/`
- Hooks & i18n: `src/hooks/`, `src/i18n/`
- Frontend Tests: `src/test/`
