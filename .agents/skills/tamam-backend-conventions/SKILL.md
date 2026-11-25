---
name: tamam-backend-conventions
description: >-
  Backend .NET 10 conventions for Tamam Workflow Studio. Trigger whenever
  creating or editing C# endpoints, handlers, domain entities, persistence code,
  or EF Core migrations.
---

# Tamam Backend Conventions (.NET 10)

## EF Core Migrations
`dotnet-ef` is pinned in `dotnet-tools.json`. Run from repo root:
```bash
dotnet ef migrations add <MigrationName> \
  --project backend/src/WorkflowEngine.Infrastructure \
  --startup-project backend/src/WorkflowEngine.Api \
  --output-dir Migrations
```
- Apply locally / verify: `dotnet ef database update --project backend/src/WorkflowEngine.Infrastructure --startup-project backend/src/WorkflowEngine.Api`
- Keep `WorkflowEngineDbContext` configurations in `backend/src/WorkflowEngine.Infrastructure/Persistence/Configurations/`.

## Security & Identity
- Extract caller identity **exclusively** from JWT `ClaimsPrincipal` (`HttpContext.User` / current-user service).
- **Never** trust `userId`, `actor`, `assignee`, or `role` supplied in the request body or query string for authorization or audit trails.
- Enforce role/ownership checks before mutating state; return `403 Forbidden` on unauthorized access.

## Concurrency & Transactions
- Use optimistic locking via entity `Version` configured with `.IsConcurrencyToken()`.
- Reject stale updates with `409 Conflict` when `DbUpdateConcurrencyException` or version mismatch occurs.
- Wrap multi-step or atomic writes in `IUnitOfWork.ExecuteInTransactionAsync(...)` so database state and side effects commit or roll back together.

## Standard HTTP Error Responses
- `400 Bad Request`: malformed payload, missing required headers/fields, invalid syntax.
- `403 Forbidden`: authenticated user lacks permission/ownership for the resource or action.
- `404 Not Found`: target entity/definition/instance/task does not exist.
- `409 Conflict`: optimistic concurrency (`Version`) conflict, duplicate key, or invalid state transition.
- `422 Unprocessable Entity`: syntactically valid payload failing domain/business validation rules.

## Context-Saving Reading Rule
- Always read C# source files in **large chunks of 250–400 lines minimum** (or full file when <= 400 lines).
- **Never** paginate files in tiny 30-line slices.
